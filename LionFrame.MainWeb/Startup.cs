using System;
using System.IO;
using System.Linq;
using AspectCore.Extensions.Autofac;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using LionFrame.Controller;
using LionFrame.CoreCommon;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace LionFrame.MainWeb
{
    public class Startup
    {
        public Startup(IWebHostEnvironment env)
        {
            var builder = new ConfigurationBuilder().SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            LionWeb.Configuration = builder.Build();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

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
                options.MaxModelValidationErrors = 3;
                //options.Filters.Add(xxx);
            })
                .SetCompatibilityVersion(CompatibilityVersion.Version_3_0)
                .AddNewtonsoftJson()
                .AddControllersAsServices();
            ;
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
                    //����֤
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

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            //autofac ���� 
            LionWeb.AutofacContainer = app.ApplicationServices.CreateScope().ServiceProvider.GetAutofacRoot();

            // autofac����
            if (LionWeb.AutofacContainer.IsRegistered<TestController>())
            {
                var testBll = LionWeb.AutofacContainer.Resolve<TestController>();
                var guid = testBll.GetGuid();
            }

            LionWeb.Configure(app.ApplicationServices.GetRequiredService<IHttpContextAccessor>());

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
    }
}