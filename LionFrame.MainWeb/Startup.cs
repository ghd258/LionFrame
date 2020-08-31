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
                    await context.Response.WriteAsync("<div style='text-align: center;margin-top: 15%;'><h3>��Ŀ<b style='color: green;'>�����ɹ�</b>,������ʹ�ýӿڲ��Թ��ߣ�����ǰ��������</h3></div>");
                });
            });
        }
    }
}
