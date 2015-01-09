#!/bin/bash

# Uses SharpDoc to generate HTML documentation for SharpNav.
# Assumes that you've cloned https://github.com/xoofx/SharpDoc in the same
# parent directory as SharpNav. You can change this along with a number of
# other parameters with the below variables.

# Will eventually reference as NuGet package, see
# https://github.com/xoofx/SharpDoc/issues/21

SHARPDOC_EXE="../../SharpDoc/src/SharpDoc/bin/Debug/SharpDoc.exe"
ASSEMBLY_DLL="../Binaries/SharpNav/Standalone/SharpNav.dll"

CONFIG_XML="../Documentation/config.xml"
OUTPUT_DIR="../Binaries/Documentation/"

if [ "$1" = "--clean" ]; then
	rm -r $OUTPUT_DIR
else
	$SHARPDOC_EXE --config=$CONFIG_XML --output=$OUTPUT_DIR $ASSEMBLY_DLL
fi
