using AspectCore.Extensions.Autofac;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using AutoMapper;
using LionFrame.Basic;
using LionFrame.Basic.Extensions;
using LionFrame.CoreCommon;
using LionFrame.CoreCommon.AutoMapperCfg;
using LionFrame.CoreCommon.Cache.Redis;
using LionFrame.CoreCommon.CustomException;
using LionFrame.CoreCommon.CustomFilter;
using LionFrame.Data.BasicData;
using LionFrame.Model;
using LionFrame.Model.ResponseDto.ResultModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;

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

            //services.AddDbContext<LionDbContext>(options =>
            //{
            //    options.UseSqlServer(connectionString: Configuration.GetConnectionString("SqlServerConnection"));
            //    options.EnableSensitiveDataLogging();
            //    options.UseLoggerFactory(loggerFactory: DbConsoleLoggerFactory);
            //});

            services.AddMemoryCache();//ʹ��MemoryCache

            // ��� automapper ӳ���ϵ
            services.AddAutoMapper(c => c.AddProfile<MappingProfile>());

            // If using Kestrel:
            services.Configure<KestrelServerOptions>(options =>
            {
                options.AllowSynchronousIO = true;

            });
            // If using IIS:
            //services.Configure<IISServerOptions>(options =>
            //{
            //    options.AllowSynchronousIO = true;
            //});
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

            services.AddRouting(options =>
            {
                options.LowercaseUrls = true; //��Դ·��Сд
            });

            #region Swagger
            // ��Ҫע�͵���Ϣ�ǵ��޸����·��  �ο�����Ŀcsproj�еĸ���,ȡ����ʾ�������1591
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo()
                {
                    Version = "v1",
                    Title = "API Doc",
                    Description = "����:Levy_w_Wang",
                    //��������
                    TermsOfService = new Uri("http://book.levy.net.cn/"),
                    //������Ϣ
                    Contact = new OpenApiContact
                    {
                        Name = "levy",
                        Email = "levywang123@gmail.com",
                        Url = new Uri("http://book.levy.net.cn/")
                    },
                    //���֤
                    License = new OpenApiLicense
                    {
                        Name = "MIT",
                        Url = new Uri("https://github.com/levy-w-wang/LionFrame/blob/master/LICENSE")
                    }
                });

                #region XmlComments

                var basePath1 = Path.GetDirectoryName(typeof(Program).Assembly.Location); //��ȡӦ�ó�������Ŀ¼�����ԣ����ܹ���Ŀ¼(ƽ̨)Ӱ�죬������ô˷�����ȡ·����
                //��ȡĿ¼�µ�XML�ļ� ��ʾע�͵���Ϣ
                var xmlComments = Directory.GetFiles(basePath1, "*.xml", SearchOption.AllDirectories).ToList();

                foreach (var xmlComment in xmlComments)
                {
                    options.IncludeXmlComments(xmlComment);
                }

                #endregion

                options.DocInclusionPredicate((docName, description) => true);

                #region ���ͷ��swaggerȫ��ͷ������

                options.AddSecurityDefinition("token", new OpenApiSecurityScheme()
                {
                    Description = "JWT Authorization header using the Bearer scheme.",//��������
                    Name = "token",//����
                    In = ParameterLocation.Header,//��Ӧλ��
                    Type = SecuritySchemeType.ApiKey,//��������
                    Scheme = "token"
                });
                //���Jwt��֤���ã���Ȼ�ڴ�����ȡ����
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference {
                                Type = ReferenceType.SecurityScheme,
                                Id = "token" }
                        }, new List<string>()
                    }
                });

                #endregion

                options.IgnoreObsoleteProperties(); //���� ��Obsolete ���Եķ���
                options.IgnoreObsoleteActions();
                options.DescribeAllEnumsAsStrings();
            });

            #endregion
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
            // ע��dbcontext������
            builder.Register(context =>
            {
                //var config = context.Resolve<IConfiguration>();
                var opt = new DbContextOptionsBuilder<LionDbContext>();
                opt.UseSqlServer(Configuration.GetConnectionString("SqlServerConnection"));

                opt.EnableSensitiveDataLogging();
                opt.UseLoggerFactory(DbConsoleLoggerFactory);

                return new LionDbContext(opt.Options);
            }).InstancePerLifetimeScope().PropertiesAutowired();

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
            // ȫ���쳣��������ַ�ʽ  ����ʹ�õ�����  //�����쳣����
            // 1.�Զ�����쳣���عܵ� - - ���ڵ�һλ����ȫ��δ��׽���쳣
            // app.UseExceptionHandler(build => build.Use(CustomExceptionHandler));
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            // 2.ʹ���Զ����쳣�����м��  ������м���Ժ�δ��׽���쳣
            //app.UseMiddleware<CustomExceptionMiddleware>();

            //autofac ���� 
            LionWeb.AutofacContainer = app.ApplicationServices.CreateScope().ServiceProvider.GetAutofacRoot();

            // autofac����
            //if (LionWeb.AutofacContainer.IsRegistered<TestController>())
            //{
            //    var testBll = LionWeb.AutofacContainer.Resolve<TestController>();
            //    var guid = testBll.GetGuid();
            //}

            LionWeb.Environment = env;
            LionWeb.Configure(app.ApplicationServices.GetRequiredService<IHttpContextAccessor>());

            LionWeb.MemoryCache = memoryCache;

            // �� http ��ת�� https
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            //app.UseCookiePolicy();

            #region Swagger

            app.UseSwagger(c =>
            {
                c.RouteTemplate = "apidoc/{documentName}/swagger.json";
            });
            app.UseSwaggerUI(c =>
            {
                c.RoutePrefix = "apidoc";
                c.SwaggerEndpoint("v1/swagger.json", "ContentCenter API V1");
                c.DocExpansion(DocExpansion.None);
            });

            #endregion

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

        #region �Զ���Ĵ������عܵ�����Ϊ�������

        /// <summary>
        /// �Զ���Ĵ������عܵ�����Ϊ�������
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        private async Task CustomExceptionHandler(HttpContext httpContext, Func<Task> next)
        {
            //����Ϣ��ExceptionHandlerMiddleware�м���ṩ�����������ExceptionHandlerMiddleware�м�����񵽵��쳣��Ϣ��
            var exceptionDetails = httpContext.Features.Get<IExceptionHandlerFeature>();
            var ex = exceptionDetails?.Error;

            if (ex != null)
            {
                LogHelper.Logger.Fatal(ex,
                    $"���쳣��Ϣ����{ex.Message} ������·������{httpContext.Request.Method}:{httpContext.Request.Path}\n " +
                    $"��UserHostAddress��:{ LionWeb.GetClientIp()} " +
                    $"��UserAgent��:{ httpContext.Request.Headers["User-Agent"]}");

                if (ex is CustomSystemException se)
                {
                    await ExceptionResult(httpContext, new ResponseModel().Fail(se.Code, se.Message, "").ToJson(true, isLowCase: true));
                }
                else if (ex is DataValidException de)
                {
                    await ExceptionResult(httpContext, new ResponseModel().Fail(de.Code, de.Message, "").ToJson(true, isLowCase: true));
                }
                else
                {
#if DEBUG
                    Console.WriteLine(ex);
                    var content = ex.ToJson();
#else
                var content = "ϵͳ�������Ժ����Ի���ϵ������Ա��";
#endif
                    await ExceptionResult(httpContext, new ResponseModel().Fail(ResponseCode.UnknownEx, content, "").ToJson(true, isLowCase: true));
                }
            }
        }

        public async Task ExceptionResult(HttpContext httpContext, string data)
        {
            httpContext.Response.StatusCode = 200;
            if (string.IsNullOrEmpty(data))
                return;
            httpContext.Response.ContentType = "application/json;charset=utf-8";
            var bytes = Encoding.UTF8.GetBytes(data);

            await httpContext.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }

        #endregion

    }
}
