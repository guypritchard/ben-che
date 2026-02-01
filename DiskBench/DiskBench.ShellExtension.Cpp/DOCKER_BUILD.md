# DiskBench C++ - Build with Docker

## Quick Start (Docker)

You can build the C++ shell extension using **pre-built Docker containers** that already have Visual Studio Build Tools installed.

### **Prerequisites**
- Docker Desktop for Windows
- ~5 GB disk space for the image (first time only)

### **Download Docker Desktop**
https://www.docker.com/products/docker-desktop

## **Three Ways to Build with Docker**

### **Method 1: Batch Script (Windows)**

```batch
cd C:\Source\ben-che\DiskBench\DiskBench.ShellExtension.Cpp
build-with-docker.bat
```

**What happens:**
1. Pulls Microsoft's official build tools image
2. Builds your DLL in the container
3. DLL appears at: `bin\Release\DiskBench.ShellExtension.Cpp.dll`
4. Done! No setup needed.

### **Method 2: PowerShell Script (Windows)**

```powershell
cd C:\Source\ben-che\DiskBench\DiskBench.ShellExtension.Cpp
.\build-with-docker.ps1
```

Same as batch script, but with PowerShell.

### **Method 3: Docker Compose**

```powershell
cd C:\Source\ben-che\DiskBench\DiskBench.ShellExtension.Cpp
docker-compose up --build
```

## **Container Images Available**

### **Option A: Microsoft Build Tools (Recommended)**
```
mcr.microsoft.com/windows/servercore:ltsc2022-with-buildtools-2022
```
- Size: ~5 GB
- Includes: MSVC, Windows SDK, MSBuild
- Build time: ~2 minutes

### **Option B: Full Visual Studio Community**
```
mcr.microsoft.com/devcontainers/cpp:windows-2022
```
- Size: ~10 GB
- Includes: VS Build Tools + extras
- Build time: ~2 minutes

### **Option C: Windows Server Core (Manual Build)**
```
mcr.microsoft.com/windows/servercore:ltsc2022
```
- Size: ~1.5 GB
- Includes: Windows only, need to install tools manually

## **How It Works**

```
Your PC
  │
  ├─ Docker Desktop (Windows)
  │
  └─ Container (Isolated Windows environment)
      ├─ Visual Studio Build Tools 2022
      ├─ MSVC Compiler
      ├─ Windows SDK
      └─ Builds your DLL
```

## **Advantages of Docker Approach**

✓ **No install needed** - Pre-built images already have everything
✓ **Clean environment** - Isolated from your system
✓ **Reproducible** - Same build every time, same container
✓ **No bloat** - Doesn't pollute your system
✓ **Easy cleanup** - Delete container, nothing left behind
✓ **Fast** - First pull ~5 minutes, then instant builds
✓ **CI/CD ready** - Same container in GitHub Actions, Azure Pipelines, etc.

## **Full Docker Workflow**

```powershell
# Step 1: Ensure Docker Desktop is running
Start-Process "C:\Program Files\Docker\Docker\Docker Desktop.exe"
Start-Sleep -Seconds 10

# Step 2: Build
cd C:\Source\ben-che\DiskBench\DiskBench.ShellExtension.Cpp
.\build-with-docker.ps1

# Step 3: Verify
Test-Path "bin\Release\DiskBench.ShellExtension.Cpp.dll"

# Step 4: Install (run as admin)
cd ..
.\Install-ExplorerCommand-Cpp.ps1
```

## **Dockerfile Breakdown**

If you want to understand what's happening:

```dockerfile
# Start with Windows Server + Build Tools
FROM mcr.microsoft.com/windows/servercore:ltsc2022-with-buildtools-2022

# Copy your source code into container
COPY DiskBench.ShellExtension.Cpp /src/DiskBench.ShellExtension.Cpp
WORKDIR /src/DiskBench.ShellExtension.Cpp

# Run MSBuild inside container
RUN "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" \
    DiskBench.ShellExtension.Cpp.vcxproj \
    /p:Configuration=Release \
    /p:Platform=x64

# Copy output to shared folder
RUN xcopy bin\Release\*.dll /output\
```

## **Troubleshooting**

### **"Docker is not running"**
```powershell
# Start Docker Desktop
Start-Process "C:\Program Files\Docker\Docker\Docker Desktop.exe"
Start-Sleep -Seconds 10
```

### **"Could not pull Docker image"**
- Check internet connection
- Ensure Docker Desktop is configured correctly
- Try: `docker pull mcr.microsoft.com/windows/servercore:ltsc2022-with-buildtools-2022`

### **"Access denied" mounting volumes**
- Ensure Docker Desktop is running in elevated mode
- Restart Docker Desktop
- Check volume mounts in Docker settings

### **Build fails inside container**
- The issue is likely in your C++ code, not Docker
- Check the error messages in the container output
- Most likely: Missing headers or invalid syntax

## **Size Comparison**

| Option | Size | Download | Build Time |
|--------|------|----------|------------|
| VS 2022 (IDE) | 10 GB | 30 min - 1 hr | Fast |
| Build Tools | 3 GB | 15-30 min | Fast |
| **Docker Image** | **5 GB** | **5-10 min** | **2-5 min** |

## **First Build vs Subsequent Builds**

**First time:**
1. Pull image: ~5-10 minutes
2. Build: ~2-5 minutes
3. **Total: ~10-15 minutes**

**Subsequent times:**
1. Image already cached
2. Build: ~2-5 minutes
3. **Total: ~2-5 minutes**

## **CI/CD Integration**

Use the same container in GitHub Actions:

```yaml
jobs:
  build:
    runs-on: windows-2022
    container:
      image: mcr.microsoft.com/windows/servercore:ltsc2022-with-buildtools-2022
    steps:
      - uses: actions/checkout@v3
      - run: |
          cd DiskBench.ShellExtension.Cpp
          msbuild DiskBench.ShellExtension.Cpp.vcxproj /p:Platform=x64
```

## **Comparison: VS 2022 vs Docker**

| Aspect | VS 2022 | Docker |
|--------|---------|--------|
| **Initial setup** | 1 hour | 10 minutes |
| **First build** | 2 min | 2-5 min |
| **Subsequent builds** | 2 min | 2-5 min |
| **IDE included** | Yes | No |
| **Space on disk** | 10+ GB | 5 GB |
| **System bloat** | Yes | No |
| **Reproducibility** | Good | Excellent |
| **Docker knowledge required** | No | Basic |

## **My Recommendation**

- **New to Docker?** Use VS 2022 Community Edition
- **Want Docker?** Use the provided scripts above
- **Have CI/CD?** Use the container approach
- **Just want it done quickly?** Either works!

## **Files Provided**

- `build-with-docker.bat` - Batch script
- `build-with-docker.ps1` - PowerShell script
- `docker-compose.yml` - Docker Compose config
- `Dockerfile` - Container definition

## **Next Steps**

1. **Install Docker Desktop** (if you don't have it)
   https://www.docker.com/products/docker-desktop

2. **Run the build script:**
   ```batch
   build-with-docker.bat
   ```

3. **Wait for the build** (~5 minutes first time, ~2 minutes after)

4. **DLL will be ready** at `bin\Release\DiskBench.ShellExtension.Cpp.dll`

5. **Install it:**
   ```powershell
   cd ..
   .\Install-ExplorerCommand-Cpp.ps1
   ```

That's it!
