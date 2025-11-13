# .NET 10.0 Upgrade Plan

## Execution Steps

Execute steps below sequentially one by one in the order they are listed.

1. Validate that a .NET 10.0 SDK required for this upgrade is installed on the machine and if not, help to get it installed.
2. Ensure that the SDK version specified in global.json files is compatible with the .NET 10.0 upgrade.
3. Upgrade src\Otapewin\Otapewin.csproj
4. Upgrade tests\Otapewin.Tests\Otapewin.Tests.csproj
5. Run unit tests to validate upgrade in the project: tests\Otapewin.Tests\Otapewin.Tests.csproj

## Settings

This section contains settings and data used by execution steps.

### Aggregate NuGet packages modifications across all projects

NuGet packages used across all selected projects or their dependencies that need version update in projects that reference them.

| Package Name                                            | Current Version | New Version | Description                                    |
|:--------------------------------------------------------|:---------------:|:-----------:|:-----------------------------------------------|
| Microsoft.Extensions.Configuration                      | 9.0.10          | 10.0.0      | Recommended for .NET 10.0                      |
| Microsoft.Extensions.Configuration.Binder               | 9.0.10          | 10.0.0      | Recommended for .NET 10.0                      |
| Microsoft.Extensions.Configuration.CommandLine          | 9.0.10          | 10.0.0      | Recommended for .NET 10.0                      |
| Microsoft.Extensions.Configuration.EnvironmentVariables | 9.0.10          | 10.0.0      | Recommended for .NET 10.0                      |
| Microsoft.Extensions.Configuration.Json                 | 9.0.10          | 10.0.0      | Recommended for .NET 10.0                      |
| Microsoft.Extensions.DependencyInjection                | 9.0.10          | 10.0.0      | Recommended for .NET 10.0                      |
| Microsoft.Extensions.Hosting                            | 9.0.10          | 10.0.0      | Recommended for .NET 10.0                      |
| Microsoft.Extensions.Logging                            | 9.0.10          | 10.0.0      | Recommended for .NET 10.0                      |
| Microsoft.Extensions.Logging.Console                    | 9.0.10          | 10.0.0      | Recommended for .NET 10.0                      |
| Microsoft.Extensions.Options.ConfigurationExtensions    | 9.0.10          | 10.0.0      | Recommended for .NET 10.0                      |
| Microsoft.Extensions.Options.DataAnnotations            | 9.0.10          | 10.0.0      | Recommended for .NET 10.0                      |
| System.Net.Http                                         | 4.3.4           |             | Package functionality included with framework  |
| System.Text.RegularExpressions                          | 4.3.1           |             | Package functionality included with framework  |

### Project upgrade details

This section contains details about each project upgrade and modifications that need to be done in the project.

#### src\Otapewin\Otapewin.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.Configuration should be updated from `9.0.10` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.Extensions.Configuration.Binder should be updated from `9.0.10` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.Extensions.Configuration.CommandLine should be updated from `9.0.10` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.Extensions.Configuration.EnvironmentVariables should be updated from `9.0.10` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.Extensions.Configuration.Json should be updated from `9.0.10` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.Extensions.DependencyInjection should be updated from `9.0.10` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.Extensions.Hosting should be updated from `9.0.10` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.Extensions.Logging should be updated from `9.0.10` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.Extensions.Logging.Console should be updated from `9.0.10` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.Extensions.Options.ConfigurationExtensions should be updated from `9.0.10` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.Extensions.Options.DataAnnotations should be updated from `9.0.10` to `10.0.0` (*recommended for .NET 10.0*)

#### tests\Otapewin.Tests\Otapewin.Tests.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.DependencyInjection should be updated from `9.0.10` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.Extensions.Hosting should be updated from `9.0.10` to `10.0.0` (*recommended for .NET 10.0*)
  - System.Net.Http version `4.3.4` should be removed (*package functionality included with framework*)
  - System.Text.RegularExpressions version `4.3.1` should be removed (*package functionality included with framework*)
