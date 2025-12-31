using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Runtime.CompilerServices;


// Debugger Displays added to core in 13.2 - https://github.com/dotnet/aspire/pull/13550
// These help with 13.1 in the meantime

[assembly: DebuggerTypeProxy(typeof(DistributedApplicationDebuggerProxy), Target = typeof(DistributedApplication))]
[assembly: DebuggerDisplay("Type = {GetType().Name,nq}, Name = {Name}, Command = {Command}", Target = typeof(ExecutableResource))]
//TODO: Container requries a bit more logic built in to aspire - wait for 13.2

namespace Common;


internal struct DistributedApplicationDebuggerProxy(DistributedApplication app)
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_host")]
    public static extern ref IHost GetHost(DistributedApplication app);

    public IHost Host => GetHost(app);

    public List<ResourceStateDebugView> Resources
    {
        get
        {
            var model = app.Services.GetRequiredService<DistributedApplicationModel>();

            var results = new List<ResourceStateDebugView>(model.Resources.Count);
            foreach (var resource in model.Resources)
            {
                //TODO: Doesn't handle replicas - requires internal `DcpInstancesAnnotation` annotations
                // But htis can be removed after 13.2
                app.ResourceNotifications.TryGetCurrentState(resource.Name, out var resourceEvent);
                results.Add(new() { Resource = resource, Snapshot = resourceEvent?.Snapshot });
            }

            return results;
        }
    }

    [DebuggerDisplay("{DebuggerToString(),nq,ac}", Name = "{Resource.Name}", Type = "{Resource.GetType().FullName,nq}")]
    internal class ResourceStateDebugView
    {
        public required IResource Resource { get; init; }

        public required CustomResourceSnapshot? Snapshot { get; init; }

        private string DebuggerToString()
        {
            var value = $@"Type = {Resource.GetType().Name}, Name = ""{Resource.Name}"", State = {Snapshot?.State?.Text ?? "(null)"}";

            if (Snapshot?.HealthStatus is { } healthStatus)
            {
                value += $", HealthStatus = {healthStatus}";
            }

            if (KnownResourceStates.TerminalStates.Contains(Snapshot?.State?.Text))
            {
                if (Snapshot?.ExitCode is { } exitCode)
                {
                    value += $", ExitCode = {exitCode}";
                }
            }

            return value;
        }
    }
}