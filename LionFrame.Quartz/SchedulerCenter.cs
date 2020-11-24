﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autofac;
using LionFrame.Basic;
using LionFrame.Basic.Extensions;
using LionFrame.Config;
using LionFrame.CoreCommon;
using LionFrame.Model;
using LionFrame.Model.QuartzModels;
using LionFrame.Model.ResponseDto.ResultModel;
using LionFrame.Quartz.Jobs;
using Quartz;

namespace LionFrame.Quartz
{
    /// <summary>
    /// 调度中心  参考网址：https://cloud.tencent.com/developer/article/1500752
    /// 数据库语句：https://github.com/quartznet/quartznet/tree/master/database/tables
    /// </summary>
    public class SchedulerCenter
    {
        /// <summary>
        /// 返回任务计划（调度器）
        /// </summary>
        /// <returns></returns>
        private IScheduler Scheduler => LionWeb.AutofacContainer.Resolve<IScheduler>();

        /// <summary>
        /// 添加一个工作调度
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public async Task<BaseResponseModel> AddScheduleJobAsync(ScheduleEntity entity)
        {
            var result = new ResponseModel<string>();
            if (!entity.Headers.IsNullOrEmpty())
            {
                try
                {
                    entity.Headers.ToObject<Dictionary<string, string>>();
                }
                catch
                {
                    result.Fail(ResponseCode.DataFormatError, "请求头参数格式错误，json字典格式", "");
                    return result;
                }
            }

            try
            {
                //检查任务是否已存在
                var jobKey = new JobKey(entity.JobName, entity.JobGroup);
                if (await Scheduler.CheckExists(jobKey))
                {
                    result.Fail("调度任务已存在", "");
                    return result;
                }

                //http请求配置
                var httpDir = new Dictionary<string, string>()
                {
                    { QuartzConstant.REQUESTURL, entity.RequestPath },
                    { QuartzConstant.REQUESTPARAMETERS, entity.RequestParameters },
                    { QuartzConstant.REQUESTTYPE, ((int) entity.RequestType).ToString() },
                    { QuartzConstant.HEADERS, entity.Headers },
                    { QuartzConstant.MAILMESSAGE, ((int) entity.MailMessage).ToString() },
                };
                // 定义这个工作，并将其绑定到我们的IJob实现类                
                IJobDetail job = JobBuilder.Create<TestJob>()
                    .SetJobData(new JobDataMap(httpDir))
                    .WithDescription(entity.Description)
                    .WithIdentity(entity.JobName, entity.JobGroup)
                    .StoreDurably() //孤立存储，指即使该JobDetail没有关联的Trigger，也会进行存储 也就是执行完成后，不删除
                    .RequestRecovery() //请求恢复，指应用崩溃后再次启动，会重新执行该作业
                    .Build();
                // 创建触发器
                ITrigger trigger;
                //校验是否正确的执行周期表达式
                if (entity.TriggerType == TriggerTypeEnum.Cron) //CronExpression.IsValidExpression(entity.Cron))
                {
                    trigger = CreateCronTrigger(entity);
                }
                else
                {
                    trigger = CreateSimpleTrigger(entity);
                }

                // 告诉Quartz使用我们的触发器来安排作业
                await Scheduler.ScheduleJob(job, trigger);
                result.Succeed("添加任务成功");
            }
            catch (Exception ex)
            {
                LogHelper.Logger.Fatal(ex, "添加任务失败", "");
                result.Fail(ResponseCode.UnknownEx, ex.Message, "");
            }

            return result;
        }

        /// <summary>
        /// 暂停/删除 指定的计划
        /// </summary>
        /// <param name="jobGroup">任务分组</param>
        /// <param name="jobName">任务名称</param>
        /// <param name="isDelete">停止并删除任务</param>
        /// <returns></returns>
        public async Task<BaseResponseModel> StopOrDelScheduleJobAsync(string jobGroup, string jobName, bool isDelete = false)
        {
            var result = new ResponseModel<string>();
            try
            {
                await Scheduler.PauseJob(new JobKey(jobName, jobGroup));
                if (isDelete)
                {
                    await Scheduler.DeleteJob(new JobKey(jobName, jobGroup));
                    result.Succeed("删除任务计划成功");
                }
                else
                {
                    result.Succeed("停止任务计划成功");
                }
            }
            catch (Exception ex)
            {
                result.Fail(ResponseCode.UnknownEx, "停止计划任务失败！--" + ex.Message, "");
            }

            return result;
        }

        /// <summary>
        /// 恢复运行暂停的任务
        /// </summary>
        /// <param name="jobName">任务名称</param>
        /// <param name="jobGroup">任务分组</param>
        public async Task<BaseResponseModel> ResumeJobAsync(string jobGroup, string jobName)
        {
            var result = new ResponseModel<string>();
            try
            {
                //检查任务是否存在
                var jobKey = new JobKey(jobName, jobGroup);
                if (await Scheduler.CheckExists(jobKey))
                {
                    //任务已经存在则暂停任务
                    await Scheduler.ResumeJob(jobKey);
                    result.Succeed("恢复任务计划成功");
                    LogHelper.Logger.Info($"任务“{jobName}”恢复运行");
                }
                else
                {
                    result.Succeed("任务不存在");
                }
            }
            catch (Exception ex)
            {
                result.Fail(ResponseCode.UnknownEx, "恢复任务计划失败！--" + ex.Message, "");
                LogHelper.Logger.Error($"恢复任务失败！{ex}");
            }

            return result;
        }

        /// <summary>
        /// 立即执行
        /// </summary>
        /// <param name="jobKey"></param>
        /// <returns></returns>
        public async Task<bool> TriggerJobAsync(JobKey jobKey)
        {
            await Scheduler.TriggerJob(jobKey);
            return true;
        }

        /// <summary>
        /// 开启调度器
        /// </summary>
        /// <returns></returns>
        public async Task<bool> StartScheduleAsync()
        {
            //开启调度器
            if (Scheduler.InStandbyMode)
            {
                await Scheduler.Start();
                LogHelper.Logger.Info("任务调度启动！");
            }

            return !Scheduler.InStandbyMode;
        }

        /// <summary>
        /// 停止任务调度
        /// </summary>
        public async Task<bool> StopScheduleAsync()
        {
            //判断调度是否已经关闭
            if (!Scheduler.InStandbyMode)
            {
                //等待任务运行完成
                await Scheduler.Standby(); //TODO  注意：Shutdown后Start会报错，所以这里使用暂停。
                LogHelper.Logger.Info("任务调度暂停！");
            }

            return Scheduler.InStandbyMode;
        }

        /// <summary>
        /// 创建类型Simple的触发器
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        private ITrigger CreateSimpleTrigger(ScheduleEntity entity)
        {
            //作业触发器
            if (entity.RunTimes.HasValue && entity.RunTimes > 0)
            {
                return TriggerBuilder.Create()
                    .WithIdentity(entity.JobName, entity.JobGroup)
                    .StartAt(entity.BeginTime) //开始时间
                    .EndAt(entity.EndTime) //结束数据
                    .WithPriority(5) // 优先级 默认为5 相同执行时间越高越先执行
                    .WithSimpleSchedule(x => 
                        x.WithIntervalInSeconds(entity.IntervalSecond ?? 1) //执行时间间隔，单位秒
                        .WithRepeatCount(entity.RunTimes.Value)) //执行次数、默认从0开始
                    .ForJob(entity.JobName, entity.JobGroup) //作业名称
                    .Build();
            }
            else
            {
                return TriggerBuilder.Create().WithIdentity(entity.JobName, entity.JobGroup).StartAt(entity.BeginTime) //开始时间
                    .WithPriority(5) // 优先级 默认为5 相同执行时间越高越先执行
                    .EndAt(entity.EndTime) //结束数据
                    .WithSimpleSchedule(x => 
                        x.WithIntervalInSeconds(entity.IntervalSecond ?? 1) //执行时间间隔，单位秒
                        .RepeatForever()) //无限循环
                    .ForJob(entity.JobName, entity.JobGroup) //作业名称
                    .Build();
            }
        }

        /// <summary>
        /// 创建类型Cron的触发器
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        private ITrigger CreateCronTrigger(ScheduleEntity entity)
        {
            // 作业触发器
            return TriggerBuilder.Create().WithIdentity(entity.JobName, entity.JobGroup).WithPriority(5) // 优先级 默认为5 相同执行时间越高越先执行
                .StartAt(entity.BeginTime) //开始时间
                .EndAt(entity.EndTime) //结束时间
                .WithCronSchedule(entity.Cron) //指定cron表达式
                .ForJob(entity.JobName, entity.JobGroup) //作业名称
                .Build();
        }
    }
}