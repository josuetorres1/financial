using System;
using MongoDB.Driver;
using Ninject;
using Ninject.Activation;
using Ninject.Web.Common;

namespace AngularJSProofofConcept
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
            return new SessionFactory(new MongoClient("mongodb://josue:josuetorres1@ds055525.mlab.com:55525/ionic-josue"));
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