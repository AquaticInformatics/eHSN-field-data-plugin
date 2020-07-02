# eHSN-field-data-plugin

[![Build status](https://ci.appveyor.com/api/projects/status/83a08te1vqco3env/branch/master?svg=true)](https://ci.appveyor.com/project/SystemsAdministrator/ehsn-field-data-plugin/branch/master)

An AQTS field data plugin supporting eHSN (electronic Hydrometric Station Notes) files from Water Survey Canada.

## Requirements

- Requires Visual Studio 2017 (Community Edition is fine)
- .NET 4.7 runtime

## Want to install this plugin?

- Install it on AQTS 2019.2-or-newer via the System Configuration page

### Plugin Compatibility Matrix

Choose the appropriate version of the plugin for your AQTS app server.

| AQTS Version | Latest compatible plugin Version |
| --- | --- |
| AQTS 2020.2 | [v20.2.0](https://github.com/AquaticInformatics/eHSN-field-data-plugin/releases/download/v20.2.0/EhsnPlugin.plugin) |
| AQTS 2020.1<br/>AQTS 2019.4<br/>AQTS 2019.3<br/>AQTS 2019.2 | [v19.2.22](https://github.com/AquaticInformatics/eHSN-field-data-plugin/releases/download/v19.2.22/EhsnPlugin.plugin) |

## Configuring the plugin

The plugin can be configured via a [`Config.json`](./src/EhsnPlugin/Config.json) JSON document, to control the mapping of eHSN values to your AQTS app server.

The JSON configuration is stored in different places, depending on the version of the plugin.

| Version | Configuration location |
| --- | --- |
| 20.2.x | Use the Settings page of the System Config app to change the settings.<br/><br/>**Group**: `FieldDataPluginConfig-EhsnPlugin`<br/>**Key**: `Config`<br/>**Value**: The entire contents of the Config.json file. If blank or omitted, the plugin's default [`Config.json`](./src/EhsnPlugin/Config.json) is used. |
| 19.2.x | Read from the `Config.json` file in the plugin folder, at `%ProgramData%\Aquatic Informatics\AQUARIUS Server\FieldDataPlugins\EhsnPlugin\Config.json` |

## Building the plugin

- Load the `src\eHSN.sln` file in Visual Studio and build the `Release` configuration.
- The `src\EhsnPlugin\deploy\Release\EhsnPlugin.plugin` file can then be installed on your AQTS app server.

## Testing the plugin within Visual Studio

Use the included `PluginTester.exe` tool from the `Aquarius.FieldDataFrame` package to test your plugin logic on the sample files.

1. Open the EhsnPlugin project's **Properties** page
2. Select the **Debug** tab
3. Select **Start external program:** as the start action and browse to `"src\packages\Aquarius.FieldDataFramework.20.2.0\tools\PluginTester.exe`
4. Enter the **Command line arguments:** to launch your plugin

```
/Plugin=EhsnPlugin.dll /Json=AppendedResults.json /Data=..\..\..\..\data\05BG006_20170609_FV.xml
```

The `/Plugin=` argument can be the filename of your plugin assembly, without any folder. The default working directory for a start action is the bin folder containing your plugin.

5. Set a breakpoint in the plugin's `ParseFile()` methods.
6. Select your plugin project in Solution Explorer and select **"Debug | Start new instance"**
7. Now you're debugging your plugin!

See the [PluginTester](https://github.com/AquaticInformatics/aquarius-field-data-framework/tree/master/src/PluginTester) documentation for more details.
