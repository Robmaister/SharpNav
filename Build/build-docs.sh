#!/bin/bash

# Uses SharpDoc to generate HTML documentation for SharpNav.
# Assumes that you've cloned https://github.com/xoofx/SharpDoc in the same
# parent directory as SharpNav. You can change this along with a number of
# other parameters with the below variables.

# Will eventually reference as NuGet package, see
# https://github.com/xoofx/SharpDoc/issues/21

##
## VARIABLES
##

SHARPDOC_EXE="../../SharpDoc/src/SharpDoc/bin/Debug/SharpDoc.exe"

SHARPNAV_DLL="../Binaries/SharpNav/Standalone/SharpNav.dll"
SHARPNAV_PROJ="./CoreOnly.proj"

CONFIG_XML="../Documentation/config.xml"
OUTPUT_DIR="../Binaries/Documentation/"

##
## HELPER FUNCTIONS
##

build_sharpnav() {
    if type msbuild >/dev/null 2>&1; then
        msbuild /t:Standalone /v:q $SHARPNAV_PROJ
    elif type xbuild >/dev/null 2>&1; then
        xbuild /t:Standalone /v:q $SHARPNAV_PROJ
    else
        echo "[ERROR] Both msbuild and xbuild are missing. Aborting."
        echo "[INFO]  If using msys on Windows, add an alias to MSBuild.exe in your .bash_profile"
        exit 1
    fi
}

##
## CLEAN
##

if [ "$1" = "--clean" ]; then
    rm -r $OUTPUT_DIR
fi

##
## BUILD
##

if [ ! -e $SHARPDOC_EXE ]; then
    echo "[ERROR] SharpDoc is not installed or is not being referenced properly."
    echo "[ERROR] Please point \$SHARPDOC_EXE to the location of SharpDoc.exe."
    exit 1
fi

if [ ! -e $SHARPNAV_DLL ]; then
    echo "[INFO]  SharpNav.dll not found, attempting to compile SharpNav."
    
    if [ -e $SHARPNAV_PROJ ]; then
        build_sharpnav
    else
        echo "[ERROR] The SharpNav project file cannot be found."
        exit 1
    fi
    
    if [ ! -e $SHARPNAV_DLL ]; then
        echo "[ERROR] SharpNav.dll cannot be found after compilation."
        exit 1
    fi
fi

if [ ! -e $CONFIG_XML ]; then
    echo "[ERROR] Missing SharpDoc configuration file."
    exit 1
fi

$SHARPDOC_EXE --config=$CONFIG_XML --output=$OUTPUT_DIR $SHARPNAV_DLL
