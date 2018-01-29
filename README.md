# eHSN-field-data-plugin

An AQTS field data plugin supporting eHSN (electronic Hydrometric Station Notes) files from Water Survey Canada.

## Requirements

- Requires Visual Studio 2017 (Community Edition is fine)
- .NET 4.7 runtime

## Building the plugin

- Load the `src\eHSN.sln` file in Visual Studio and build the `Release` configuration.
- The `deploy\Release\eHSN.plugin` file can then be installed on your AQTS app server.

## Installation of the plugin

Use the [FieldDataPluginTool](https://github.com/AquaticInformatics/Examples/tree/master/TimeSeries/PublicApis/FieldDataPlugins/FieldDataPluginTool) to install the plugin on your AQTS app server.
