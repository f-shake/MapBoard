try {
    # 输出信息模块化
    function Show-Message {
        param ([string]$Message)
        Write-Output $Message
    }

    Show-Message "请先阅读ReadMe"
    Show-Message "请确保："
    Show-Message "已经安装.NET 8 SDK"
    Show-Message "已经安装Microsoft Visual C++ 2015-2019 Redistributable"

    pause
    Clear-Host

    # 检查.NET SDK
    try {
        dotnet
    } catch {
        throw "未安装.NET SDK"
    }

    Clear-Host

    # 清理发布目录
    if (Test-Path Generation/Publish) {
        Remove-Item Generation/Publish -Recurse -ErrorAction SilentlyContinue
    }

    # 发布WPF应用
    function Publish-WPF {
        param (
            [string]$OutputDir,
            [bool]$SelfContained,
            [bool]$SingleFile
        )
        
        Show-Message "正在发布WPF到 $OutputDir"
        dotnet publish MapBoard.UI -c Release -o $OutputDir -r win-x64 --self-contained $SelfContained /p:PublishSingleFile=$SingleFile
    }

    # 复制DLL文件
    function Copy-DLLFiles {
        param ([string]$DestinationDir)
        Copy-Item "Generation/Publish/WPF_Standard/Extension.*.dll" $DestinationDir
        #Copy-Item C:\Users\$($env:USERNAME)\.nuget\packages\esri.arcgisruntime.runtimes.win\200.4.0\runtimes\win-x64\native\* $DestinationDir
        #Copy-Item C:\Users\$($env:USERNAME)\.nuget\packages\esri.arcgisruntime.wpf\200.4.0\runtimes\win-x64\native\* $DestinationDir
    }

    Publish-WPF "Generation/Publish/WPF_Standard" $false $false
    Publish-WPF "Generation/Publish/WPF" $false $true
    Publish-WPF "Generation/Publish/WPF_Contained" $true $true

    # 复制DLL文件
    Copy-DLLFiles "Generation/Publish/WPF"
    Copy-DLLFiles "Generation/Publish/WPF_Contained"
    

    Show-Message "正在清理"
    Remove-Item Generation/Release -Recurse
    Remove-Item Generation/Publish/WPF_Standard -Recurse

    Show-Message "操作完成"

    Invoke-Item Generation/Publish
    pause
} catch {
    Write-Error $_
}



