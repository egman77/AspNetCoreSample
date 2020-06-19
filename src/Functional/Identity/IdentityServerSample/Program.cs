using System;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityServerSample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) => CreateDefaultBuilder(args).UseStartup<Startup>();

        private static IWebHostBuilder CreateDefaultBuilder(string[] args)
        {
            var builder = new WebHostBuilder();

            if (string.IsNullOrEmpty(builder.GetSetting(WebHostDefaults.ContentRootKey)))  //"contentRoot"
            {
                builder.UseContentRoot(Directory.GetCurrentDirectory());//当前目录
            }
            if (args != null)
            {
                builder.UseConfiguration(new ConfigurationBuilder().AddCommandLine(args).Build()); //附加配置文件
            }

            //使用小的host服务器 kestrel
            builder.UseKestrel((builderContext, options) =>
            {
                options.Configure(builderContext.Configuration.GetSection("Kestrel")); //配置kestrel
            })
            .ConfigureAppConfiguration((hostingContext, config) => //配置应用程序
            {
                var env = hostingContext.HostingEnvironment; //host的环境

                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true); //加载配置文件

                if (env.IsDevelopment())
                {
                    //调试模式时, 使用当前环境 程序集
                    var appAssembly = Assembly.Load(new AssemblyName(env.ApplicationName));
                    if (appAssembly != null)
                    {
                        config.AddUserSecrets(appAssembly, optional: true);
                    }
                }

                config.AddEnvironmentVariables(); //使用环境变量

                if (args != null)
                {
                    config.AddCommandLine(args); //配置命令行
                }
            })
            .ConfigureLogging((hostingContext, logging) =>
            {
                logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging")).AddConsole().AddDebug(); //使用日志,配置日志信息
            })
            .ConfigureServices((hostingContext, services) =>
            {
                // Fallback  应急计划
                services.PostConfigure<HostFilteringOptions>(options =>
                    {
                        //使用白名单
                        if (options.AllowedHosts == null || options.AllowedHosts.Count == 0)
                        {
                            // "AllowedHosts": "localhost;127.0.0.1;[::1]"
                            var hosts = hostingContext.Configuration["AllowedHosts"]?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                            // Fall back to "*" to disable.
                            options.AllowedHosts = (hosts?.Length > 0 ? hosts : new[] { "*" });
                        }
                    });
                
                // Change notification 改变通知?
                services.AddSingleton<IOptionsChangeTokenSource<HostFilteringOptions>>(
                    new ConfigurationChangeTokenSource<HostFilteringOptions>(hostingContext.Configuration));

                services.AddTransient<IStartupFilter, HostFilteringStartupFilter>(); //启动过滤器
            })
            .UseDefaultServiceProvider((context, options) =>
            {
                options.ValidateScopes = context.HostingEnvironment.IsDevelopment(); //验证作用域?
            });
            return builder;
        }
    }
    internal class HostFilteringStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.UseHostFiltering();
                next(app);
            };
        }
    }
}
