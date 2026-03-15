# Путь к файлу с номером сборки
$buildFilePath = "$PSScriptRoot\BuildNumber.txt"

# Чтение текущего номера сборки
$currentBuildNumber = [int](Get-Content $buildFilePath)

# Увеличение номера сборки
$currentBuildNumber++

# Запись нового номера сборки обратно в файл
$currentBuildNumber | Set-Content $buildFilePath

# Установка номера сборки в свойство MSBuild
Write-Host "##vso[task.setvariable variable=BuildNumber]$currentBuildNumber"
