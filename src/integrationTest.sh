#!/bin/bash

Configuration=$1
PluginTesterPath=$2

[ ! -z "$Configuration" ] || Configuration=Debug
[ ! -z "$PluginTesterPath" ] || PluginTesterPath=packages/Aquarius.FieldDataFramework.18.4.2/tools

mkdir -p ../results

$PluginTesterPath/PluginTester.exe -Plugin=EhsnPlugin/bin/$Configuration/EhsnPlugin.dll -Data=../data/*.xml -Json=../results
