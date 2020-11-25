using AspectCore.Extensions.Autofac;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using AutoMapper;
using LionFrame.CoreCommon;
using LionFrame.CoreCommon.AutoMapperCfg;
using LionFrame.CoreCommon.Cache;
using LionFrame.CoreCommon.Cache.Redis;
using LionFrame.CoreCommon.CustomFilter;
using LionFrame.Data.BasicData;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Quartz;
using Z.EntityFramework.Extensions;
using LionFrame.Quartz;

namespace LionFrame.MainWeb
{
    public partial class Startup
    {
        public Startup(IWebHostEnvironment env)
        {
            var builder = new ConfigurationBuilder().SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            var config = builder.Build();
            LionWeb.Configuration = config;
            Configuration = config;
        }

        public IConfiguration Configuration { get; set; }
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            ConfigureServices_Db(services);

            services.AddMemoryCache();//ʹ��MemoryCache

            // ��� AutoMapper ӳ���ϵ
            services.AddAutoMapper(c => c.AddProfile<MappingProfile>());

            services.AddHttpClient<IHttpClientBuilder>();
            // If using Kestrel:
            services.Configure<KestrelServerOptions>(options =>
            {
                options.AllowSynchronousIO = true;

            });
            // If using IIS:
            services.Configure<IISServerOptions>(options =>
            {
                options.AllowSynchronousIO = true;
            });

            services.AddControllers(options =>
            {
                //��֤���������������ⷵ��һ��������
                options.MaxModelValidationErrors = 3;
                // 3.�쳣������--����mvc��δ��׽���쳣
                options.Filters.Add<ExceptionFilter>();
            })
                .SetCompatibilityVersion(CompatibilityVersion.Version_3_0)
                .ConfigureApiBehaviorOptions(options =>
                {
                    //����ϵͳ�Դ�ģ����֤,��Ȼֻ���� ApiController �Դ���ģ����֤
                    options.SuppressModelStateInvalidFilter = true;
                })
                .AddNewtonsoftJson()
                .AddControllersAsServices();
            //��Դ·��Сд
            services.AddRouting(options =>
            {
                options.LowercaseUrls = true;
            });

#if !DEBUG
              ConfigureServices_HealthChecks(services); //��������²������������
#endif
            ConfigureServices_Swagger(services);
        }

        /// <summary>
        /// �°�autofacע��
        /// </summary>
        /// <param name="builder"></param>
        public void ConfigureContainer(ContainerBuilder builder)
        {
            builder.RegisterModule<AutofacModule>();
            // ע��redisʵ��
            builder.RegisterInstance(new RedisClient(Configuration)).SingleInstance().PropertiesAutowired();
            builder.RegisterInstance(new LionMemoryCache("Cache")).SingleInstance().PropertiesAutowired();
            var scheduler = new SchedulerFactory(Configuration).GetScheduler().Result;
            builder.RegisterInstance(scheduler).As<IScheduler>().PropertiesAutowired().SingleInstance();

            #region ע��dbcontext������ ʹ������ע�� -- ����ʹ������ķ�ʽֱ��add����Ҳ����

            //builder.Register(context =>
            //{
            //    //var config = context.Resolve<IConfiguration>();
            //    var opt = new DbContextOptionsBuilder<LionDbContext>();
            //    opt.UseSqlServer(Configuration.GetConnectionString("SqlServerConnection"));
            //    opt.ReplaceService<IMigrationsModelDiffer, MigrationsModelDifferWithoutForeignKey>();
            //    opt.EnableSensitiveDataLogging();
            //    opt.UseLoggerFactory(DbConsoleLoggerFactory);

            //    return new LionDbContext(opt.Options);
            //}).InstancePerLifetimeScope().PropertiesAutowired();

            #endregion

            // ����AOP����
            // ����ע��ðѷ������� virtual  ��ȻûЧ��
            builder.RegisterDynamicProxy(config =>
            {
                ////ȫ��ʹ��AOP  �������ڲ���ʹ�õĽӿڵķ�ʽ����Ҫ��Ҫʹ��AOP�ķ����ϼ� virtual �ؼ���
                //config.Interceptors.AddTyped<LogInterceptorAttribute>();
                //config.Interceptors.AddServiced<LogInterceptorAttribute>();
                //// ����Service��׺��ǰ�����ᱻ����
                //config.Interceptors.AddTyped<LogInterceptorAttribute>(method => method.Name.EndsWith("Service"));
                //// ʹ�� ͨ��� ���ض�ȫ��������
                //config.Interceptors.AddTyped<LogInterceptorAttribute>(Predicates.ForService("*Service"));

                ////Demo.Data�����ռ��µ�Service���ᱻ����
                //config.NonAspectPredicates.AddNamespace("Demo.Data");

                ////���һ��ΪData�������ռ��µ�Service���ᱻ����
                //config.NonAspectPredicates.AddNamespace("*.Data");

                ////ICustomService�ӿڲ��ᱻ����
                //config.NonAspectPredicates.AddService("ICustomService");

                ////��׺ΪService�Ľӿں��಻�ᱻ����
                //config.NonAspectPredicates.AddService("*Service");

                ////����ΪFindUser�ķ������ᱻ����
                //config.NonAspectPredicates.AddMethod("FindUser");

                ////��׺ΪUser�ķ������ᱻ����
                //config.NonAspectPredicates.AddMethod("*User");
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IMemoryCache memoryCache)
        {
            // ȫ���쳣��������ַ�ʽ
            // 1.�Զ�����쳣���عܵ� - - ���ڵ�һλ����ȫ��δ��׽���쳣
            app.UseExceptionHandler(build => build.Use(CustomExceptionHandler));
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
                app.UseHttpsRedirection();// �� http ��ת�� https
                Config_HealthChecks(app); //��������²������������
            }

            // 2.ʹ���Զ����쳣�����м��  ������м���Ժ�δ��׽���쳣
            //app.UseMiddleware<CustomExceptionMiddleware>();

            //autofac ���� 
            LionWeb.AutofacContainer = app.ApplicationServices.CreateScope().ServiceProvider.GetAutofacRoot();

            // Z.EntityFramework.Extensions ��չ����Ҫ  --�޷���ʾ��־
            EntityFrameworkManager.ContextFactory = context => LionWeb.AutofacContainer.Resolve<LionDbContext>();

            LionWeb.Environment = env;
            LionWeb.Configure(LionWeb.AutofacContainer.Resolve<IHttpContextAccessor>());

            LionWeb.MemoryCache = memoryCache;

            app.UseStaticFiles();
            //app.UseCookiePolicy();

            Config_Swagger(app);

            app.UseRouting();
            //app.UseRequestLocalization();

            // ����
            app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod().WithMethods(new string[]
            {
                HttpMethods.Get,
                HttpMethods.Post,
                HttpMethods.Delete,
                HttpMethods.Put
            }));

            // app.UseSession();
            // app.UseResponseCaching();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapGet("/", async context =>
                {
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.WriteAsync("<div style='text-align: center;margin-top: 15%;'><h3>��Ŀ<b style='color: green;'>�����ɹ�</b>,������ʹ�ýӿڲ��Թ��ߣ�����ǰ��������</h3> <h4>��Ŀ<a href='/apidoc' style='color: cornflowerblue;'>�ӿ��ĵ�</a>,����鿴</h4></div>");
                });
            });
        }
    }
}
