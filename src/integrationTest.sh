#!/bin/bash

Configuration=$1
PluginTesterPath=$2

[ ! -z "$Configuration" ] || Configuration=Debug
[ ! -z "$PluginTesterPath" ] || PluginTesterPath=packages/Aquarius.FieldDataFramework.18.4.1/tools

mkdir -p ../results

for f in ../data/*.xml; do
	$PluginTesterPath/PluginTester.exe -Plugin=EhsnPlugin/bin/$Configuration/EhsnPlugin.dll -Data=$f -Json=${f/\/data\///results/}.json >/dev/null && echo "GOOD: $f" || echo "ERROR: $f" `grep --max-count=1 ERROR $PluginTesterPath/PluginTester.log` | tee ${f/\/data\///results/}.error.log
done
