using System;
using System.Configuration;
using Artez.Core;
using Artez.Core.Events;
using Artez.Persistence;
using Ninject;
using Ninject.Activation;
using Ninject.Web.Common;

namespace Artez.Web.DependencyInjection
{
    public static class UnitOfWorkDependencies
    {
        public static void BindToKernel(IKernel kernel, Type unitOfWorkFactoryDecoratorType = null)
        {
            kernel.Bind<IArtezSessionFactory>().ToMethod(CreateMsSqlSessionFactory).InSingletonScope();
            kernel.Bind<IArtezSession>().ToMethod(CreateArtezSession).InRequestScope();

            var binding = kernel.Bind<IUnitOfWorkFactory>().ToMethod(CreateUnitOfWorkFactory);

            if (unitOfWorkFactoryDecoratorType != null)
            {
                binding.WhenInjectedInto(unitOfWorkFactoryDecoratorType);
                kernel.Bind<IUnitOfWorkFactory>().To(unitOfWorkFactoryDecoratorType);
            }
        }

        private static IArtezSessionFactory CreateMsSqlSessionFactory(IContext context)
        {
            var domainEventRaiser = context.Kernel.Get<IDomainEventRaiser>();

            return new MsSqlSessionFactory(
                ConfigurationManager.ConnectionStrings["dbConnectionString"].ConnectionString,
                domainEventRaiser);
        }

        private static IArtezSession CreateArtezSession(IContext context)
        {
            return context.Kernel.Get<IArtezSessionFactory>().Create();
        }

        private static IUnitOfWorkFactory CreateUnitOfWorkFactory(IContext context)
        {
            return context.Kernel.Get<IArtezSession>().CreateUnitOfWorkFactory();
        }
    }
}