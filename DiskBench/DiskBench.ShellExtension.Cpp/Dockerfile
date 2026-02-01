# Dockerfile for building DiskBench C++ Shell Extension
FROM mcr.microsoft.com/windows/servercore:ltsc2022

# Install Visual Studio Build Tools
RUN powershell -Command \
    Invoke-WebRequest -Uri "https://aka.ms/vs/17/release/vs_buildtools.exe" `
    -OutFile "vs_buildtools.exe" ; \
    .\vs_buildtools.exe --quiet --wait --norestart `
    --add Microsoft.VisualStudio.Workload.VCTools `
    --add Microsoft.VisualStudio.Component.Windows10SDK.19041 ; \
    del vs_buildtools.exe

# Copy source code
COPY DiskBench.ShellExtension.Cpp /src/DiskBench.ShellExtension.Cpp
WORKDIR /src/DiskBench.ShellExtension.Cpp

# Build
RUN "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" ^
    DiskBench.ShellExtension.Cpp.vcxproj ^
    /p:Configuration=Release ^
    /p:Platform=x64

# Output DLL to /output
RUN xcopy bin\Release\*.dll /output\

CMD cmd /c echo "Build complete! DLL is in /output/"
