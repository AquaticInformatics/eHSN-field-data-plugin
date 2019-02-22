rem Regenerate the eHSN XML serialization classes from the schema.
rem Assumes that the Microsoft Visual Studio 201x Tool XSD.EXE is accessible on the PATH

xsd EhsnPlugin\Schema\eHSN.xsd /out:EhsnPlugin\Schema /classes
