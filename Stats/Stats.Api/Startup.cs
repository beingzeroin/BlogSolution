﻿using BlogSolution.Mongo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Autofac.Extensions.DependencyInjection;
using System;
using BlogSolution.Authentication;
using BlogSolution.Mvc;
using Stats.Application.Modules;
using Autofac;
using BlogSolution.Types.Settings;
using BlogSolution.Shared.Options;
using BlogSolution.EventBusRabbitMQ;
using BlogSolution.EventBus.Abstractions;
using Stats.Api.IntegrationEvents.EventHandlers;
using Stats.Api.IntegrationEvents.Events;
using Stats.Api.IntegrationEvents;
using BlogSolution.Consul;
using Consul;

namespace Stats.Api
{
    public class Startup
    {
        public IContainer Container { get; private set; }
        public IConfiguration Configuration { get; }


        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddApiBehaviorOptions();
            services.AddJwt();
            services.AddOption<AppSettings>("app");
            services.AddCustomMvc();
            services.AddIntegrationServices();
            services.AddEventBus();
            services.AddEventHandlers();
            services.AddServiceDiscovery();
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", cors =>
                        cors.AllowAnyOrigin()
                            .AllowAnyMethod()
                            .AllowAnyHeader()
                            .AllowCredentials());
            });
            var builder = new ContainerBuilder();
            builder.Populate(services);
            builder.AddMongo();
            builder.RegisterModule(new ApplicationModule());
            builder.RegisterModule(new ValidatorModule());
            Container = builder.Build();
            return new AutofacServiceProvider(Container);

        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime applicationLifetime, IConsulClient consulClient)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors("CorsPolicy");
            app.UseAllForwardedHeaders();
            app.UseErrorHandler();
            app.UseAuthentication();
            app.UseMvc();
            ConfigureEventBus(app);
            var serviceId = app.UseConsulRegisterService();

            applicationLifetime.ApplicationStopped.Register(() =>
            {
                consulClient.Agent.ServiceDeregister(serviceId);
                Container.Dispose();
            });
        }

        private void ConfigureEventBus(IApplicationBuilder app)
        {
            var eventBus = app.ApplicationServices.GetRequiredService<IEventBus>();
            eventBus.Subscribe<PostDeletedIntegrationEvent, PostDeletedIntegrationEventHandler>();
        }
    }
}
