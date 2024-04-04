#!/bin/bash

Configuration=$1
PluginTesterPath=$2

[ ! -z "$Configuration" ] || Configuration=Debug
[ ! -z "$PluginTesterPath" ] || PluginTesterPath=packages/Aquarius.FieldDataFramework.23.4.2/tools

rm -rf ../data-parsed
mkdir -p ../data-parsed
$PluginTesterPath/PluginTester.exe -Plugin=EhsnPlugin/bin/$Configuration/EhsnPlugin.dll -Data=../data/*.xml -Json=../data-parsed

if [ $(diff -rq ../data-parsed ../data-parsed-expected | grep -v 'Only in ../data-parsed:' | wc -l) -gt 0 ];
then
    echo ERROR: difference found between data-parsed and data-parsed-expected
    diff -rq ../data-parsed ../data-parsed-expected | grep -v 'Only in ../data-parsed:'
    exit 1
fi