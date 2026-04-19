using System.Runtime.CompilerServices;

// Expose internal members to test assemblies for unit testing
[assembly: InternalsVisibleTo("UnitTests")]
[assembly: InternalsVisibleTo("EditModeTests")]
