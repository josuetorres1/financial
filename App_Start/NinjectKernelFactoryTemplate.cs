using System;
using Ninject;
using Ninject.Activation;
using NLog.Interface;
using LogManager = NLog.LogManager;

namespace AngularJSProofofConcept
{
    public abstract class NinjectKernelFactoryTemplate
    {
        public IKernel Create()
        {
            var kernel = new StandardKernel();

            kernel.Bind<Func<IKernel>>().ToMethod(ctx => () => kernel);

            kernel.Bind<ILogger>().ToMethod(CreateLogger);

            RegisterServices(kernel);

            return kernel;
        }

        private static ILogger CreateLogger(IContext context)
        {
            var declaringType = context.Request.Target.Member.DeclaringType;
            var fullName = declaringType == null ? "" : declaringType.FullName;
            return new LoggerAdapter(LogManager.GetLogger(fullName));
        }

        protected abstract void RegisterServices(IKernel kernel);
    }
}