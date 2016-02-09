using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using DryIoc.UnitTests.CUT;
using NUnit.Framework;

namespace DryIoc.UnitTests
{
    [TestFixture]
    public class RulesTests
    {
        [Test]
        public void Given_service_with_two_ctors_I_can_specify_what_ctor_to_choose_for_resolve()
        {
            var container = new Container();

            container.Register(typeof(Bla<>), made: Made.Of(
                t => t.GetConstructorOrNull(args: new[] { typeof(Func<>).MakeGenericType(t.GetGenericParamsAndArgs()[0]) })));

            container.Register(typeof(SomeService), typeof(SomeService));

            var bla = container.Resolve<Bla<SomeService>>();

            Assert.That(bla.Factory(), Is.InstanceOf<SomeService>());
        }

        [Test]
        public void I_should_be_able_to_add_rule_to_resolve_not_registered_service()
        {
            var container = new Container(Rules.Default.WithUnknownServiceResolvers(request =>
                !request.ServiceType.IsValueType() && !request.ServiceType.IsAbstract()
                    ? new ReflectionFactory(request.ServiceType)
                    : null));

            var service = container.Resolve<NotRegisteredService>();

            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void I_can_remove_rule_to_resolve_not_registered_service()
        {
            Rules.UnknownServiceResolver unknownServiceResolver = request =>
                !request.ServiceType.IsValueType() && !request.ServiceType.IsAbstract()
                    ? new ReflectionFactory(request.ServiceType)
                    : null;
            
            IContainer container = new Container(Rules.Default.WithUnknownServiceResolvers(unknownServiceResolver));
            Assert.IsNotNull(container.Resolve<NotRegisteredService>());

            container = container
                .With(rules => rules.WithoutUnknownServiceResolver(unknownServiceResolver))
                .WithoutCache(); // Important to remove cache

            Assert.IsNull(container.Resolve<NotRegisteredService>(IfUnresolved.ReturnDefault));
        }
        
        [Test]
        public void When_service_registered_with_name_Then_it_could_be_resolved_with_ctor_parameter_ImportAttribute()
        {
            var container = new Container(rules => rules.With(parameters: GetServiceInfoFromImportAttribute));

            container.Register(typeof(INamedService), typeof(NamedService));
            container.Register(typeof(INamedService), typeof(AnotherNamedService), serviceKey: "blah");
            container.Register(typeof(ServiceWithImportedCtorParameter));

            var service = container.Resolve<ServiceWithImportedCtorParameter>();

            Assert.That(service.NamedDependency, Is.InstanceOf<AnotherNamedService>());
        }

        [Test]
        public void I_should_be_able_to_import_single_service_based_on_specified_metadata()
        {
            var container = new Container(rules => rules.With(parameters: GetServiceFromWithMetadataAttribute));

            container.Register(typeof(IFooService), typeof(FooHey), setup: Setup.With(metadataOrFuncOfMetadata: FooMetadata.Hey));
            container.Register(typeof(IFooService), typeof(FooBlah), setup: Setup.With(metadataOrFuncOfMetadata: FooMetadata.Blah));
            container.Register(typeof(FooConsumer));

            var service = container.Resolve<FooConsumer>();

            Assert.That(service.Foo.Value, Is.InstanceOf<FooBlah>());
        }

        [Test]
        public void You_can_specify_rules_to_resolve_last_registration_from_multiple_available()
        {
            var container = new Container(Rules.Default.WithFactorySelector(Rules.SelectLastRegisteredFactory()));

            container.Register(typeof(IService), typeof(Service));
            container.Register(typeof(IService), typeof(AnotherService));
            var service = container.Resolve(typeof(IService));

            Assert.That(service, Is.InstanceOf<AnotherService>());
        }

        [Test]
        public void You_can_specify_rules_to_disable_registration_based_on_reuse_type()
        {
            var container = new Container(Rules.Default.WithFactorySelector(
                (request, factories) => factories.FirstOrDefault(f => f.Key.Equals(request.ServiceKey) && !(f.Value.Reuse is SingletonReuse)).Value));

            container.Register<IService, Service>(Reuse.Singleton);
            var service = container.Resolve(typeof(IService), IfUnresolved.ReturnDefault);

            Assert.That(service, Is.Null);
        }

        public static Func<ParameterInfo, ParameterServiceInfo> GetServiceInfoFromImportAttribute(Request request)
        {
            return parameter =>
            {
                var import = (ImportAttribute)parameter.GetAttributes(typeof(ImportAttribute)).FirstOrDefault();
                var details = import == null ? ServiceDetails.Default
                    : ServiceDetails.Of(import.ContractType, import.ContractName);
                return ParameterServiceInfo.Of(parameter).WithDetails(details, request);
            };
        }

        public static Func<ParameterInfo, ParameterServiceInfo> GetServiceFromWithMetadataAttribute(Request request)
        {
            return parameter =>
            {
                var import = (ImportWithMetadataAttribute)parameter.GetAttributes(typeof(ImportWithMetadataAttribute))
                    .FirstOrDefault();
                if (import == null)
                    return null;

                var registry = request.Container;
                var serviceType = parameter.ParameterType;
                serviceType = registry.GetWrappedType(serviceType, request.RequiredServiceType);
                var metadata = import.Metadata;
                var factory = registry.GetAllServiceFactories(serviceType)
                    .FirstOrDefault(kv => metadata.Equals(kv.Value.Setup.Metadata))
                    .ThrowIfNull();

                return ParameterServiceInfo.Of(parameter)
                    .WithDetails(ServiceDetails.Of(serviceType, factory.Key), request);
            };
        }

        [Test]
        public void Can_turn_Off_singleton_optimization()
        {
            var container = new Container(r => r.WithoutEagerCachingSingletonForFasterAccess());
            container.Register<FooHey>(Reuse.Singleton);

            var singleton = container.Resolve<LambdaExpression>(typeof(FooHey));

            Assert.That(singleton.ToString(), Is.StringContaining("SingletonScope"));
        }

        internal class XX { }
        internal class YY { }
        internal class ZZ { }

        [Test]
        public void AutoFallback_resolution_rule_should_respect_IfUnresolved_policy_in_case_of_multiple_registrations()
        {
            var container = new Container()
                .WithAutoFallbackResolution(new[] { typeof(Me), typeof(MiniMe) }, 
                (reuse, request) => reuse == Reuse.Singleton ? null : reuse);

            var me = container.Resolve<IMe>(IfUnresolved.ReturnDefault);

            Assert.IsNull(me);
        }

        [Test] 
        public void AutoFallback_resolution_rule_should_respect_IfUnresolved_policy_in_case_of_multiple_registrations_from_assemblies()
        {
            var container = new Container()
                .WithAutoFallbackResolution(new[] { typeof(Me).GetAssembly() },
                (reuse, request) => reuse == Reuse.Singleton ? null : reuse);

            var me = container.Resolve<IMe>(IfUnresolved.ReturnDefault);

            Assert.IsNull(me);
        }

        [Test]
        public void You_may_specify_condition_to_exclude_unwanted_services_from_AutoFallback_resolution_rule()
        {
            var container = new Container()
                .WithAutoFallbackResolution(new[] { typeof(Me) }, 
                condition: request => request.Parent.ImplementationType.Name.Contains("Green"));

            container.Register<RedMe>();

            Assert.IsNull(container.Resolve<RedMe>(IfUnresolved.ReturnDefault));
        }

        public interface IMe {}
        internal class Me : IMe {}
        internal class MiniMe : IMe {}
        internal class GreenMe { public GreenMe(IMe me) {} }
        internal class RedMe { public RedMe(IMe me) { } }

        [Test]
        public void Exist_support_for_non_primitive_value_injection_via_container_rule()
        {
            var container = new Container(rules => rules.WithItemToExpressionConverter(
                (item, type) => type == typeof(ConnectionString)
                ? Expression.New(type.GetSingleConstructorOrNull(),
                    Expression.Constant(((ConnectionString)item).Value))
                : null));

            var s = new ConnectionString("aaa");
            container.Register(Made.Of(() => new ConStrUser(Arg.Index<ConnectionString>(0)), r => s));

            var user = container.Resolve<ConStrUser>();
            Assert.AreEqual("aaa", user.S.Value);
        }

        [Test]
        public void Container_rule_for_serializing_custom_value_to_expression_should_throw_proper_exception_for_not_supported_type()
        {
            var container = new Container(rules => rules.WithItemToExpressionConverter(
                (item, type) => type == typeof(ConnectionString)
                ? Expression.New(type.GetSingleConstructorOrNull(),
                    Expression.Constant(((ConnectionString)item).Value))
                : null));

            var s = new ConnectionStringImpl("aaa");
            container.Register(Made.Of(() => new ConStrUser(Arg.Index<ConnectionString>(0)), r => s));

            var ex = Assert.Throws<ContainerException>(() => container.Resolve<ConStrUser>());
            Assert.AreEqual(Error.StateIsRequiredToUseItem, ex.Error);
        }

        public class ConnectionString
        {
            public string Value;
            public ConnectionString(string value)
            {
                Value = value;
            }
        }

        public class ConnectionStringImpl : ConnectionString {
            public ConnectionStringImpl(string value) : base(value) {}
        }

        public class ConStrUser 
        {
            public ConnectionString S { get; set; }
            public ConStrUser(ConnectionString s)
            {
                S = s;
            }
        }

        [Test]
        public void Container_should_throw_on_registering_disposable_transient()
        {
            var container = new Container();

            var ex = Assert.Throws<ContainerException>(() => 
                container.Register<AD>());

            Assert.AreEqual(Error.RegisteredDisposableTransientWontBeDisposedByContainer, ex.Error);
        }

        [Test]
        public void I_can_silence_throw_on_registering_disposable_transient_for_specific_registration()
        {
            var container = new Container();

            Assert.DoesNotThrow(() => 
            container.Register<AD>(setup: Setup.With(allowDisposableTransient: true)));
        }

        [Test]
        public void I_can_silence_throw_on_registering_disposable_transient_for_whole_container()
        {
            var container = new Container(rules => rules.WithoutThrowOnRegisteringDisposableTransient());

            Assert.DoesNotThrow(() => 
            container.Register<AD>());
        }

        [Test]
        public void Should_track_transient_disposable_dependency_in_singleton_scope()
        {
            var container = new Container(rules => rules.WithTrackingDisposableTransients());

            container.Register<AD>();
            container.Register<ADConsumer>(Reuse.Singleton);
            var singleton = container.Resolve<ADConsumer>();

            container.Dispose();

            Assert.IsTrue(singleton.Ad.IsDisposed);
        }

        [Test]
        public void Should_not_track_func_of_transient_disposable_dependency_in_singleton_scope()
        {
            var container = new Container(rules => rules.WithTrackingDisposableTransients());

            container.Register<AD>();
            container.Register<ADFuncConsumer>(Reuse.Singleton);
            var singleton = container.Resolve<ADFuncConsumer>();

            container.Dispose();

            Assert.IsFalse(singleton.Ad.IsDisposed);
        }

        [Test]
        public void Should_track_lazy_of_transient_disposable_dependency_in_singleton_scope()
        {
            var container = new Container(rules => rules.WithTrackingDisposableTransients());

            container.Register<AD>();
            container.Register<ADLazyConsumer>(Reuse.Singleton);
            var singleton = container.Resolve<ADLazyConsumer>();

            container.Dispose();

            Assert.IsTrue(singleton.Ad.IsDisposed);
        }

        [Test]
        public void Should_track_transient_disposable_dependency_in_current_scope()
        {
            var container = new Container(rules => rules.WithTrackingDisposableTransients());

            container.Register<AD>();
            container.Register<ADConsumer>(Reuse.InCurrentScope);

            ADConsumer scoped;
            using (var scope = container.OpenScope())
            {
                scoped = scope.Resolve<ADConsumer>();
            }

            Assert.IsTrue(scoped.Ad.IsDisposed);
        }

        [Test]
        public void Should_track_transient_disposable_dependency_in_resolution_scope()
        {
            var container = new Container(rules => rules.WithTrackingDisposableTransients());

            container.Register<AD>(setup: Setup.With(asResolutionCall: true));
            container.Register<ADConsumer>(Reuse.InResolutionScopeOf<AResolutionScoped>(), setup: Setup.With(asResolutionCall: true));
            container.Register<AResolutionScoped>(setup: Setup.With(openResolutionScope: true));

            var scoped = container.Resolve<AResolutionScoped>();
            scoped.Dependencies.Dispose();

            Assert.IsTrue(scoped.Consumer.Ad.IsDisposed);
        }

        [Test]
        public void Should_track_transient_service_in_open_scope_if_present()
        {
            var container = new Container();
            container.Register<AD>(setup: Setup.With(trackDisposableTransient: true));

            AD ad;
            using (var scope = container.OpenScope())
                ad = scope.Resolve<AD>();

            Assert.IsTrue(ad.IsDisposed);
        }

        [Test]
        public void Should_track_transient_service_in_open_scope_of_any_name_if_present()
        {
            var container = new Container();
            container.Register<AD>(setup: Setup.With(trackDisposableTransient: true));

            AD ad;
            using (var scope = container.OpenScope("hey"))
                ad = scope.Resolve<AD>();

            Assert.IsTrue(ad.IsDisposed);
        }


        [Test]
        public void Should_NOT_track_transient_service_in_singleton_scope_if_no_open_scope_because_it_is_most_definitely_a_leak()
        {
            var container = new Container();
            container.Register<AD>(setup: Setup.With(trackDisposableTransient: true));

            var ad = container.Resolve<AD>();

            container.Dispose();
            Assert.IsFalse(ad.IsDisposed);
        }

        public class AD : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        public class ADConsumer
        {
            public AD Ad { get; private set; }

            public AD Ad2 { get; private set; }

            public ADConsumer(AD ad, AD ad2)
            {
                Ad = ad;
                Ad2 = ad2;
            }
        }

        public class AResolutionScoped
        {
            public ADConsumer Consumer { get; private set; }

            public IDisposable Dependencies { get; private set; }

            public AResolutionScoped(ADConsumer consumer, IDisposable dependencies)
            {
                Consumer = consumer;
                Dependencies = dependencies;
            }
        }

        public class AResolutionScopedConsumer
        {
            public AResolutionScoped AScoped { get; private set; }

            public IDisposable Dependencies { get; private set; }

            public AResolutionScopedConsumer(AResolutionScoped aScoped, IDisposable dependencies)
            {
                AScoped = aScoped;
                Dependencies = dependencies;
            }
        }

        public class ADFuncConsumer
        {
            public AD Ad { get; private set; }

            public ADFuncConsumer(Func<AD> ad)
            {
                Ad = ad();
            }
        }

        public class ADLazyConsumer
        {
            public AD Ad { get; private set; }

            public ADLazyConsumer(Lazy<AD> ad)
            {
                Ad = ad.Value;
            }
        }

        [Test]
        public void Can_specify_IfAlreadyRegistered_per_Container()
        {
            var container = new Container(rules => rules
                .WithDefaultIfAlreadyRegistered(IfAlreadyRegistered.Keep));

            container.Register<I, A>();
            container.Register<I, B>();

            var i = container.Resolve<I>();

            Assert.IsInstanceOf<A>(i);
        }

        [Test]
        public void If_IfAlreadyRegistered_per_Container_is_overriden_by_individual_registrations()
        {
            var container = new Container(rules => rules
                .WithDefaultIfAlreadyRegistered(IfAlreadyRegistered.Keep));

            container.Register<I, A>();
            container.Register<I, B>(ifAlreadyRegistered: IfAlreadyRegistered.Replace);

            var i = container.Resolve<I>();

            Assert.IsInstanceOf<B>(i);
        }

        [Test]
        public void If_IfAlreadyRegistered_per_Container_affects_RegisterMany_as_expected()
        {
            var container = new Container(rules => rules
                .WithDefaultIfAlreadyRegistered(IfAlreadyRegistered.Keep));

            container.RegisterMany(new[] { typeof(A), typeof(B)});

            var i = container.Resolve<I>();

            Assert.IsInstanceOf<A>(i);
        }

        public interface I { }

        public class A : I { }

        public class B : I { }

        #region CUT

        public class SomeService { }

        public class Bla<T>
        {
            public string Message { get; set; }
            public Func<T> Factory { get; set; }

            public Bla(string message)
            {
                Message = message;
            }

            public Bla(Func<T> factory)
            {
                Factory = factory;
            }
        }

        enum FooMetadata { Hey, Blah }

        public interface IFooService
        {
        }

        public class FooHey : IFooService
        {
        }

        public class FooBlah : IFooService
        {
        }

        [AttributeUsage(AttributeTargets.Parameter)]
        public class ImportWithMetadataAttribute : Attribute
        {
            public ImportWithMetadataAttribute(object metadata)
            {
                Metadata = metadata.ThrowIfNull();
            }

            public readonly object Metadata;
        }

        public class FooConsumer
        {
            public Lazy<IFooService> Foo { get; set; }

            public FooConsumer([ImportWithMetadata(FooMetadata.Blah)] Lazy<IFooService> foo)
            {
                Foo = foo;
            }
        }

        public class TransientOpenGenericService<T>
        {
            public T Value { get; set; }
        }

        public interface INamedService
        {
        }

        public class NamedService : INamedService
        {
        }

        public class AnotherNamedService : INamedService
        {
        }

        public class ServiceWithImportedCtorParameter
        {
            public INamedService NamedDependency { get; set; }

            public ServiceWithImportedCtorParameter([Import("blah")]INamedService namedDependency)
            {
                NamedDependency = namedDependency;
            }
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        class NotRegisteredService
        {
        }

        #endregion
    }
}