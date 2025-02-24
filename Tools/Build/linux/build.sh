#!/usr/bin/env bash

###
### Copyright (c) 2022-2023 Carbon Community 
### All rights reserved
###
set -e

echo "** Get the base path of the script"
BASE="$(cd -- "$(dirname "$0")" >/dev/null 2>&1; pwd -P)"
ROOT="$(realpath "${BASE}/../../../")"

TARGET=${1:-Debug}
echo "** Set the build target config to ${TARGET}"

echo "** Cleanup the release folder"
rm -rf "${ROOT}/Release/.tmp/${TARGET}" "${ROOT}/Release/Carbon.${TARGET}.tar.gz" || exit 0

echo "** Build the solution"
dotnet restore "${ROOT}/Carbon.Core" -v:m --nologo
dotnet   clean "${ROOT}/Carbon.Core" -v:m --configuration ${TARGET} --nologo
dotnet   build "${ROOT}/Carbon.Core" -v:m --configuration ${TARGET} --no-restore --no-incremental /p:UserConstants="${DEFINES}"

echo "** Copy operating system specific files"
if [[ "${TARGET}" == *"Unix"* ]]; then
	cp "${ROOT}/Tools/Helpers/environment.sh"                 "${ROOT}/Release/.tmp/${TARGET}/carbon/tools"
	cp "${ROOT}/Tools/UnityDoorstop/linux/x64/libdoorstop.so" "${ROOT}/Release/.tmp/${TARGET}/libdoorstop.so"
else
	cp "${ROOT}/Tools/Helpers/doorstop_config.ini"            "${ROOT}/Release/.tmp/${TARGET}"
	cp "${ROOT}/Tools/UnityDoorstop/windows/x64/doorstop.dll" "${ROOT}/Release/.tmp/${TARGET}/winhttp.dll"
fi

if [[ "${TARGET}" == *"Unix" ]]; then
	if [[ "${TARGET}" == "Debug"* ]]; then
		TOS=Linux
		TAG=Debug
	else
		TOS=Linux
		TAG=Release
	fi
else
	if [[ "${TARGET}" == "Debug"* ]]; then
		TOS=Windows
		TAG=Debug
	else
		TOS=Windows
		TAG=Release
	fi
fi

if [ "${2}" != "--no-archive" ]; then
	echo "** Create the compressed archive"
	tar -zcvf "${ROOT}/Release/Carbon.${TOS}.${TAG}.tar.gz" -C "${ROOT}/Release/.tmp/${TARGET}" $(ls -A ${ROOT}/Release/.tmp/${TARGET})
	mono "${ROOT}/Tools/BuildInfo/Carbon.BuildInfo.exe" -carbon "${ROOT}/Release/.tmp/${TARGET}/carbon/managed/Carbon" -o "${ROOT}/Release/Carbon.${TOS}.${TAG}.info"
fi