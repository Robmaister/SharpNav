#!/bin/bash

MSBUILD_EXE="C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"
BUILD_FILE="../CoreOnly.proj"
declare -a CONFIGS=("Standalone" "OpenTK" "MonoGame" "SharpDX")

BUILD_TOOL="xbuild"

command -v $BUILD_TOOL >/dev/null 2>&1
XBUILD_EXISTS=$?

if [ -f "$MSBUILD_EXE" ]; then
	BUILD_TOOL="$MSBUILD_EXE"
elif [ $XBUILD_EXISTS -ne 0 ]; then
	echo "Neither MSBuild nor XBuild are available on this system. Aborting."
	exit 1
fi

echo ""
echo "                BUILDING                 "
echo "========================================="
echo ""
for config in "${CONFIGS[@]}"
do
	echo "$config"
	echo "---------------------------"
	"$BUILD_TOOL" "$BUILD_FILE" "//m" "//nologo" "//v:m" "//clp:Summary" "//p:configuration=$config"
	if [ $? -ne 0 ]; then
		echo -e "\033[0;31mMSBuild exited with an error, aborting.\033[0m"
		exit
	fi
	echo "---------------------------"
	echo ""
done

echo ""
echo "                 PACKING                 "
echo "========================================="
echo ""
for f in *.nuspec
do
	echo "${f%.*}"
	echo "---------------------------"
	nuget Pack $f
	echo "---------------------------"
	echo ""
done

echo ""
echo "               PUBLISHING                "
echo "========================================="
echo ""
echo "Packages ready to publish:"
for f in *.nupkg
do
	echo "  - ${f%.*}"
done
echo ""

read -r -p "Are you sure you want to publish these packages? [y/N] " response
case $response in
	[yY][eE][sS]|[yY])
		;;
	*)
		echo "Aborted. Exiting..."
		exit 1
		;;
esac

for f in *.nupkg
do
	nuget Push $f
done

echo ""
echo "Done."
echo ""
