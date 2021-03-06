﻿using Spear.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spear.Codec.MessagePack;
using Spear.Codec.ProtoBuffer;
using Spear.Core;
using Spear.Core.Config;
using Spear.Core.Micro;
using Spear.Core.Micro.Services;
using Spear.Protocol.Grpc;
using Spear.Protocol.Http;
using Spear.Protocol.Tcp;
using Spear.Protocol.WebSocket;
using Spear.ProxyGenerator;
using Spear.Tests.Contracts;
using Spear.Tests.Server.Services;
using System;
using System.Threading.Tasks;

namespace Spear.Tests.Server
{
    internal class Program
    {
        private static IServiceProvider _provider;

        private static void Main(string[] args)
        {
            var port = -1;
            bool? gzip = null;
            if (args.Length > 0)
                int.TryParse(args[0], out port);
            var protocol = ServiceProtocol.Tcp;
            var codec = ServiceCodec.Json;

            if (args.Length > 1)
                protocol = args[1].CastTo(ServiceProtocol.Tcp);
            if (args.Length > 2)
                codec = args[2].CastTo(ServiceCodec.Json);
            if (args.Length > 3)
                gzip = args[3].CastTo(false);

            //ConfigHelper.Instance.UseLocal("_config");

            Console.WriteLine("shay".Config<string>());

            var services = new MicroBuilder();
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddFilter("System", level => level >= LogLevel.Warning);
                builder.AddFilter("Microsoft", level => level >= LogLevel.Warning);
                builder.AddConsole();
            });

            services
                .AddMicroService(builder =>
                {
                    switch (codec)
                    {
                        case ServiceCodec.Json:
                            builder.AddJsonCodec();
                            break;
                        case ServiceCodec.MessagePack:
                            builder.AddMessagePackCodec();
                            break;
                        case ServiceCodec.ProtoBuf:
                            builder.AddProtoBufCodec();
                            break;
                    }

                    builder
                        .AddSession()
                        //.AddNacos()
                        //.AddConsul()
                        .AddDefaultRouter();
                    ;

                    switch (protocol)
                    {
                        case ServiceProtocol.Tcp:
                            builder.AddTcpProtocol();
                            break;
                        case ServiceProtocol.Http:
                            builder.AddHttpProtocol();
                            break;
                        case ServiceProtocol.Ws:
                            builder.AddWebSocketProtocol();
                            break;
                        case ServiceProtocol.Grpc:
                            builder.AddGrpcProtocol();
                            break;
                    }
                })
                .AddMicroClient(builder =>
                {
                    //支持多编解码&多协议
                    builder
                        .AddJsonCodec()
                        .AddMessagePackCodec()
                        .AddProtoBufCodec()
                        .AddHttpProtocol()
                        .AddTcpProtocol()
                        .AddWebSocketProtocol()
                        .AddGrpcProtocol()
                        .AddSession()
                        .AddDefaultRouter()
                        //.AddConsul("http://192.168.0.231:8500")
                        ;
                });
            services.AddSingleton<ITestContract, TestService>();
            services.AddScoped<AccountService>();

            _provider = services.BuildServiceProvider();
            //_provider.UseNacosConfig();

            _provider.UseMicroService(address =>
            {
                var m = "micro".Config<ServiceAddress>();
                if (m == null) return;
                address.Service = m.Service;
                address.Host = m.Host;
                address.Port = port > 80 ? port : m.Port;
                if (address.Port < 80)
                    address.Port = 5000;
                address.Weight = m.Weight;
                address.Gzip = gzip ?? m.Gzip;
            });
            AppDomain.CurrentDomain.ProcessExit += async (sender, eventArgs) => await Shutdown();
            Console.CancelKeyPress += async (sender, eventArgs) =>
            {
                await Shutdown();
                eventArgs.Cancel = true;
            };
            var proxy = _provider.GetService<IProxyFactory>();
            var contract = proxy.Create<ITestContract>();
            var result = contract.Get("shay");
            Console.WriteLine(result.Result);
            while (true)
            {
                var cmd = Console.ReadLine();
                if (cmd == "exit") break;
            }
        }

        private static async Task Shutdown()
        {
            await _provider.GetService<IMicroHost>().Stop();
        }
    }
}
