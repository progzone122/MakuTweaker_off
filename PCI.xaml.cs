using MakuTweakerNew.Properties;
using MicaWPF.Core.Enums;
using MicaWPF.Core.Services;
using Microsoft.Win32;
using NvAPIWrapper.GPU;
using NvAPIWrapper.Native;
using NvAPIWrapper.Native.GPU;
using SharpGen.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Vortice.DXGI;
using Vortice.Mathematics;
using Windows.Devices.Portable;

namespace MakuTweakerNew
{
    public partial class PCI : Page
    {
        private dynamic _pci;
        bool isLoaded = false;
        bool isNotify = true;
        bool isbycheck = false;
        bool isCompactMode = false;
        MainWindow mw = (MainWindow)Application.Current.MainWindow;
        private List<GpuInfo> _gpus = new List<GpuInfo>();
        private List<StorageInfo> _storageDevices = new List<StorageInfo>();
        private List<RamStickInfo> _ramSticks = new();
        private DateTime lastCompactToggle = DateTime.MinValue;

        public PCI()
        {
            Environment.SetEnvironmentVariable("LHM_NO_RING0", "1");
            InitializeComponent();
            this.PreviewKeyDown += PCI_PreviewKeyDown;
            LoadLang();
            ShowRamInfo();
            ShowCpuInfo();
            ShowCpuExtraInfo();
            ShowMotherboardInfo();
            LoadGpuList();
            LoadStorageList();
            ShowComputerInfo();
            ShowSecurityInfo();
            LoadRamSticks();
            isLoaded = true;
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        void FadeOut(UIElement element)
        {
            if (element.Visibility != Visibility.Visible)
                return;
            if (element.ReadLocalValue(UIElement.OpacityProperty) != DependencyProperty.UnsetValue)
                return;

            DoubleAnimation fade = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(250),
                FillBehavior = FillBehavior.Stop
            };

            fade.Completed += (s, e) =>
            {
                element.BeginAnimation(UIElement.OpacityProperty, null);
                element.Opacity = 1;
                element.Visibility = Visibility.Hidden;
            };

            element.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        private void PCI_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                SaveDataToTxt();
                FadeOut(buttontooltip);
            }
            if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && e.Key == Key.S)
            {
                SaveDataToTxt();
                FadeOut(buttontooltip);
                e.Handled = true;
            }
            if (e.Key == Key.F3)
            {
                if ((DateTime.Now - lastCompactToggle).TotalSeconds < 1)
                    return;

                lastCompactToggle = DateTime.Now;

                ToggleCompactMode();
                e.Handled = true;
            }
        }

        private void ToggleCompactMode()
        {
            isCompactMode = !isCompactMode;
            ApplyCompactMode();
        }

        private void ApplyCompactStorage(dynamic pci)
        {
            if (_storageDevices == null || _storageDevices.Count == 0)
            {
                ssdcValue.Text = "N/A";
                return;
            }

            ulong totalBytes = 0;
            List<string> parts = new();

            foreach (var drive in _storageDevices)
            {
                totalBytes += drive.CapacityBytes;

                string type = string.IsNullOrWhiteSpace(drive.Type) ? "" : drive.Type + " ";
                parts.Add($"{type}{drive.CapacityFormatted}");
            }

            double totalTB = totalBytes / (1024.0 * 1024 * 1024 * 1024);

            List<string> lines = new();
            for (int i = 0; i < parts.Count; i += 2)
            {
                lines.Add(string.Join(" + ", parts.Skip(i).Take(2)));
            }

            string breakdown = string.Join(Environment.NewLine, lines);
            ssdcValue.Text = $"{totalTB:0.##} TB ({breakdown})";
        }

        private void ApplyCompactGpu(dynamic pci)
        {
            if (_gpus == null || _gpus.Count == 0)
            {
                videol.Text = pci["main"]["full_gpu"];
                videon.Text = "N/A";
                return;
            }

            var gpu = _gpus.OrderByDescending(g => g.VRamBytes).First();

            videol.Text = pci["main"]["full_gpu"];
            videon.Text = $"{gpu.Name} // {gpu.VRamFormatted}";
        }

        private void ApplyCompactMode()
        {
            var pci = MainWindow.Localization.LoadLocalization(
                Properties.Settings.Default.lang ?? "en", "pci");

            if (isCompactMode)
            {
                bmanu.Text = pci["main"]["full_model"];
                cpul.Text = pci["main"]["full_cpu"];
                videol.Text = pci["main"]["full_gpu"];
                raml.Text = pci["main"]["full_ram"];
                mbnamel.Text = pci["main"]["full_motherboard"];
                ssdcLabel.Text = pci["main"]["full_usbssd"];
                rama.Text = $"{rama.Text} // {ddre.Text} // {freq.Text}";
                cpue.Text = $"{cpue.Text} // {cpucore.Text} // {threads.Text}";
                pcManufacturer.Text = $"{pcManufacturer.Text} {pcModel.Text}";
                biosver.Text = $"{biosver.Text} // {biosdate.Text}".Replace(" // N/A", "");

                ApplyCompactGpu(pci);
                ApplyCompactStorage(pci);

                labelcpu.Visibility = Visibility.Collapsed;
                labelRAM.Visibility = Visibility.Collapsed;
                video.Visibility = Visibility.Collapsed;
                ssdLabel.Visibility = Visibility.Collapsed;
                ramslabel.Visibility = Visibility.Collapsed;
                MOTHERBOARD.Visibility = Visibility.Collapsed;
                btitle.Visibility = Visibility.Collapsed;
                benchmarkSection.Visibility = Visibility.Collapsed;

                videoComboBox.Visibility = Visibility.Collapsed;
                ramStickComboBox.Visibility = Visibility.Collapsed;
                ssdComboBox.Visibility = Visibility.Collapsed;
                ramStickSection.Visibility = Visibility.Collapsed;

                biosDateRow.Visibility = Visibility.Collapsed;

                ddrl.Visibility = Visibility.Collapsed;
                ddre.Visibility = Visibility.Collapsed;
                freql.Visibility = Visibility.Collapsed;
                freq.Visibility = Visibility.Collapsed;

                cpucorel.Visibility = Visibility.Collapsed;
                cpucore.Visibility = Visibility.Collapsed;
                threadsl.Visibility = Visibility.Collapsed;
                threads.Visibility = Visibility.Collapsed;
                corespeedl.Visibility = Visibility.Collapsed;
                corespeed.Visibility = Visibility.Collapsed;
                l3cashl.Visibility = Visibility.Collapsed;
                l3cash.Visibility = Visibility.Collapsed;

                vraml.Visibility = Visibility.Collapsed;
                vram.Visibility = Visibility.Collapsed;

                bmodel.Visibility = Visibility.Collapsed;
                pcModel.Visibility = Visibility.Collapsed;

                ssdnLabel.Visibility = Visibility.Collapsed;
                ssdnValue.Visibility = Visibility.Collapsed;

                cpuCoreRow.Visibility = Visibility.Collapsed;
                cpuThreadRow.Visibility = Visibility.Collapsed;
                cpuSpeedRow.Visibility = Visibility.Collapsed;
                cpuCacheRow.Visibility = Visibility.Collapsed;
                gpuVramRow.Visibility = Visibility.Collapsed;
                ramTypeRow.Visibility = Visibility.Collapsed;
                ramFreqRow.Visibility = Visibility.Collapsed;
                ssdNameRow.Visibility = Visibility.Collapsed;

                FadeOut(buttontooltip);
            }
            else
            {
                LoadLang();
                ShowCpuInfo();
                ShowRamInfo();
                ShowComputerInfo();
                LoadGpuList();
                LoadStorageList();
                ShowMotherboardInfo();

                labelcpu.Visibility = Visibility.Visible;
                labelRAM.Visibility = Visibility.Visible;
                video.Visibility = Visibility.Visible;
                ssdLabel.Visibility = Visibility.Visible;
                ramslabel.Visibility = Visibility.Visible;
                MOTHERBOARD.Visibility = Visibility.Visible;
                btitle.Visibility = Visibility.Visible;
                benchmarkSection.Visibility = Visibility.Visible;

                videoComboBox.Visibility = Visibility.Visible;
                ramStickComboBox.Visibility = Visibility.Visible;
                ssdComboBox.Visibility = Visibility.Visible;
                ramStickSection.Visibility = Visibility.Visible;

                biosDateRow.Visibility = Visibility.Visible;

                ddrl.Visibility = Visibility.Visible;
                ddre.Visibility = Visibility.Visible;
                freql.Visibility = Visibility.Visible;
                freq.Visibility = Visibility.Visible;

                cpucorel.Visibility = Visibility.Visible;
                cpucore.Visibility = Visibility.Visible;
                threadsl.Visibility = Visibility.Visible;
                threads.Visibility = Visibility.Visible;
                corespeedl.Visibility = Visibility.Visible;
                corespeed.Visibility = Visibility.Visible;
                l3cashl.Visibility = Visibility.Visible;
                l3cash.Visibility = Visibility.Visible;

                vraml.Visibility = Visibility.Visible;
                vram.Visibility = Visibility.Visible;

                bmodel.Visibility = Visibility.Visible;
                pcModel.Visibility = Visibility.Visible;

                ssdnLabel.Visibility = Visibility.Visible;
                ssdnValue.Visibility = Visibility.Visible;

                cpuCoreRow.Visibility = Visibility.Visible;
                cpuThreadRow.Visibility = Visibility.Visible;
                cpuSpeedRow.Visibility = Visibility.Visible;
                cpuCacheRow.Visibility = Visibility.Visible;
                gpuVramRow.Visibility = Visibility.Visible;
                ramTypeRow.Visibility = Visibility.Visible;
                ramFreqRow.Visibility = Visibility.Visible;
                ssdNameRow.Visibility = Visibility.Visible;
            }
        }

        private async Task RunBenchmarkAsync(bool runMultithreadedByDefault)
        {
            singleBench.IsEnabled = false;
            multiBench.IsEnabled = false;
            lookresults.IsEnabled = false;
            mw.Category.IsEnabled = false;
            ssdComboBox.IsEnabled = false;
            videoComboBox.IsEnabled = false;
            ramStickComboBox.IsEnabled = false;

            const int benchmarkDurationMilliseconds = 10_000;
            var pci = MainWindow.Localization.LoadLocalization(Properties.Settings.Default.lang ?? "en", "pci");
            bool isMultithreaded = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) || runMultithreadedByDefault;

            benchmarkResultText.Text = isMultithreaded
                ? $"{pci["main"]["running_multicore"]}"
                : $"{pci["main"]["running"]}";

            var result = await Task.Run(() =>
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                long totalOps = 0;

                if (isMultithreaded)
                {
                    int threads = Environment.ProcessorCount;
                    long[] threadOps = new long[threads];

                    Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, i =>
                    {
                        double a = 1.000001 + i * 0.00001;
                        double b = 1.000002 + i * 0.00002;
                        long x = 1234567 + i;
                        long localOps = 0;
                        var rnd = new Random(i * 37 + Environment.TickCount);

                        while (stopwatch.ElapsedMilliseconds < benchmarkDurationMilliseconds)
                        {
                            for (int k = 0; k < 200_000; k++)
                            {
                                a = Math.Sin(a) * Math.Cos(b) + Math.Sqrt(Math.Abs(a + b));
                                b = a * 0.999999 + b * 0.000001 + rnd.NextDouble();
                                x = (x * 1664525 + 1013904223) & 0xFFFFFFFF;
                                localOps += 3;
                            }
                        }

                        threadOps[i] = localOps;
                    });

                    totalOps = threadOps.Sum();
                }
                else
                {
                    double a = 1.000001;
                    double b = 1.000002;
                    long x = 1234567;
                    long ops = 0;
                    var rnd = new Random(Environment.TickCount);

                    while (stopwatch.ElapsedMilliseconds < benchmarkDurationMilliseconds)
                    {
                        for (int k = 0; k < 200_000; k++)
                        {
                            a = Math.Sin(a) * Math.Cos(b) + Math.Sqrt(Math.Abs(a + b));
                            b = a * 0.999999 + b * 0.000001 + rnd.NextDouble();
                            x = (x * 1664525 + 1013904223) & 0xFFFFFFFF;
                            ops += 3;
                        }
                    }

                    totalOps = ops;
                }

                stopwatch.Stop();

                double seconds = stopwatch.Elapsed.TotalSeconds;
                double score = (totalOps / seconds) / 100000.0;

                return (score, stopwatch.ElapsedMilliseconds);
            });

            string scoreText = $"{result.score:N0}";

            benchmarkResultText.Text = isMultithreaded
                ? $"{pci["main"]["test1multi"]}\n{pci["main"]["test2"]} {scoreText} {pci["main"]["test3"]}"
                : $"{pci["main"]["test1"]}\n{pci["main"]["test2"]} {scoreText} {pci["main"]["test3"]}";

            singleBench.IsEnabled = true;
            multiBench.IsEnabled = true;
            lookresults.IsEnabled = true;
            mw.Category.IsEnabled = true;
            ssdComboBox.IsEnabled = true;
            videoComboBox.IsEnabled = true;
            ramStickComboBox.IsEnabled = true;
        }

        private async void singleBench_Click(object sender, RoutedEventArgs e)
        {
            await RunBenchmarkAsync(false);
        }

        private async void multiBench_Click(object sender, RoutedEventArgs e)
        {
            await RunBenchmarkAsync(true);
        }

        private void LoadLang()
        {
            var languageCode = Properties.Settings.Default.lang ?? "en";
            _pci = MainWindow.Localization.LoadLocalization(languageCode, "pci");

            label.Text = _pci["main"]["label"];

            labelcpu.Text = _pci["main"]["processorlabel"];
            cpul.Text = _pci["main"]["processorname"];
            cpucorel.Text = _pci["main"]["processorcores"];
            threadsl.Text = _pci["main"]["processorthr"];
            corespeedl.Text = _pci["main"]["processorfreq"];
            l3cashl.Text = _pci["main"]["processorcache"];

            labelRAM.Text = _pci["main"]["ramlabel"];
            raml.Text = _pci["main"]["ramtotal"];
            ddrl.Text = _pci["main"]["ramddr"];
            freql.Text = _pci["main"]["ramfreq"];

            MOTHERBOARD.Text = _pci["main"]["mblabel"];
            mbnamel.Text = _pci["main"]["mbname"];
            biosverl.Text = _pci["main"]["mbver"];
            biosdatel.Text = _pci["main"]["mbdate"];

            video.Text = _pci["main"]["vlabel"];
            videol.Text = _pci["main"]["vname"];
            vraml.Text = _pci["main"]["vmem"];

            ssdLabel.Text = _pci["main"]["ssdl"];
            ssdnLabel.Text = _pci["main"]["sname"];
            ssdcLabel.Text = _pci["main"]["smem"];

            benchmarkLabel.Text = _pci["main"]["benchtitle"];
            singleBench.Content = _pci["main"]["benchbutton"];
            multiBench.Content = _pci["main"]["benchbutton2"];
            benchmarkResultText.Text = _pci["main"]["benchtip"] + "\n";
            lookresults.Content = _pci["main"]["lookresulbutton"];

            btitle.Text = _pci["main"]["branding"];
            bmanu.Text = _pci["main"]["manu"];
            bmodel.Text = _pci["main"]["modeln"];
            tpml.Text = _pci["main"]["tpmtitle"];

            ramslabel.Text = _pci["main"]["ramsticktitle"];
            ramsmanu.Text = _pci["main"]["manu"];
            capacram.Text = _pci["main"]["capac"];
            partnuml.Text = _pci["main"]["partnum"];

            pci_tooltip.Content = _pci["main"]["tooltip"];
        }

        private void ShowTpmStatus(bool enabled)
        {
            if (_pci == null)
            {
                tpmStatus.Text = enabled ? "Enabled" : "Disabled";
                return;
            }

            tpmStatus.Text = enabled
                ? _pci["main"]["tpmy"]
                : _pci["main"]["tpmn"];
        }

        private void ShowCpuInfo()
        {
            try
            {
                string cpuName = "Unknown";
                int coreCount = 0;
                int threadCount = 0;

                using (var searcher = new ManagementObjectSearcher("select Name, NumberOfCores, NumberOfLogicalProcessors from Win32_Processor"))
                using (var results = searcher.Get())
                {
                    foreach (var item in results)
                    {
                        cpuName = item["Name"]?.ToString()?.Trim() ?? cpuName;
                        coreCount += Convert.ToInt32(item["NumberOfCores"] ?? 0);
                        threadCount += Convert.ToInt32(item["NumberOfLogicalProcessors"] ?? 0);
                        item.Dispose();
                    }
                }

                Dispatcher.Invoke(() => {
                    cpue.Text = cpuName;
                    cpucore.Text = coreCount.ToString();
                    threads.Text = threadCount.ToString();
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => {
                    cpue.Text = "Error reading CPU";
                    cpucore.Text = "N/A";
                    threads.Text = "N/A";
                });
            }
        }

        private void ShowCpuExtraInfo()
        {
            try
            {
                int maxClockSpeed = 0;
                int l3Cache = 0;

                using (var searcher = new ManagementObjectSearcher("select MaxClockSpeed, L3CacheSize from Win32_Processor"))
                {
                    foreach (var item in searcher.Get())
                    {
                        maxClockSpeed = Convert.ToInt32(item["MaxClockSpeed"]);
                        l3Cache += Convert.ToInt32(item["L3CacheSize"]);
                    }
                }

                double l3MB = Math.Round(l3Cache / 1024.0, 1);
                double maxGHz = Math.Round(maxClockSpeed / 1000.0, 2);

                corespeed.Text = $"{maxGHz} GHz";
                l3cash.Text = $"{l3MB} MB";
            }
            catch (Exception ex)
            {
                corespeed.Text = $"{ex.Message}";
                l3cash.Text = "N/A";
            }
        }
        private void ShowRamInfo()
        {
            try
            {
                ulong totalBytes = 0;
                int memoryTypeCode = 0;
                int maxSpeed = 0;

                using (var searcher = new ManagementObjectSearcher("SELECT Capacity, MemoryType, SMBIOSMemoryType, Speed FROM Win32_PhysicalMemory"))
                {
                    using (var results = searcher.Get())
                    {
                        foreach (ManagementObject item in results)
                        {
                            try
                            {
                                if (item["Capacity"] != null)
                                    totalBytes += (ulong)item["Capacity"];

                                int smbios = item["SMBIOSMemoryType"] != null ? Convert.ToInt32(item["SMBIOSMemoryType"]) : 0;
                                int legacy = item["MemoryType"] != null ? Convert.ToInt32(item["MemoryType"]) : 0;

                                int detectedType = smbios != 0 ? smbios : legacy;
                                if (memoryTypeCode == 0 && detectedType != 0)
                                    memoryTypeCode = detectedType;

                                if (item["Speed"] != null)
                                    maxSpeed = Math.Max(maxSpeed, Convert.ToInt32(item["Speed"]));
                            }
                            finally
                            {
                                item.Dispose();
                            }
                        }
                    }
                }

                if (totalBytes == 0)
                {
                    Dispatcher.Invoke(() => {
                        rama.Text = "N/A";
                        ddre.Text = "N/A";
                        freq.Text = "N/A";
                    });
                    return;
                }
                double totalGB = totalBytes / (1024.0 * 1024 * 1024);
                string memoryType = memoryTypeCode switch
                {
                    20 => "DDR",
                    21 => "DDR2",
                    22 => "DDR2 FB-DIMM",
                    24 => "DDR3",
                    26 => "DDR4",
                    31 => "DDR5",
                    27 => "LPDDR",
                    28 => "LPDDR2",
                    29 => "LPDDR3",
                    30 => "LPDDR4",
                    32 => "LPDDR5",
                    33 => "LPDDR5X",
                    34 => "LPDDR5",
                    35 => "LPDDR5X",
                    _ => "N/A"
                };

                if (memoryType == "N/A" && maxSpeed > 0)
                {
                    if (maxSpeed >= 7000) memoryType = "LPDDR5X";
                    else if (maxSpeed >= 5500) memoryType = "LPDDR5";
                    else if (maxSpeed >= 4200) memoryType = "LPDDR4X";
                    else if (maxSpeed >= 3200) memoryType = "DDR4";
                }

                int realSpeed = maxSpeed;
                if (realSpeed > 0)
                {
                    if (memoryType == "DDR4" && realSpeed > 4000) realSpeed /= 2;
                    else if (memoryType == "DDR3" && realSpeed > 3000) realSpeed /= 2;
                    else if (memoryType.StartsWith("LPDDR5")) realSpeed = Math.Min(realSpeed, 6400);
                }
                Dispatcher.Invoke(() => {
                    rama.Text = $"{Math.Round(totalGB)} GB";
                    ddre.Text = memoryType;
                    freq.Text = realSpeed > 0 ? $"{realSpeed} MHz" : "N/A";
                });
            }
            catch
            {
                Dispatcher.Invoke(() => {
                    rama.Text = "N/A";
                    ddre.Text = "N/A";
                    freq.Text = "N/A";
                });
            }
        }

        private void ShowMotherboardInfo()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Product, Manufacturer FROM Win32_BaseBoard"))
                {
                    foreach (var item in searcher.Get())
                    {
                        string product = item["Product"]?.ToString() ?? "Unknown";
                        string manufacturer = item["Manufacturer"]?.ToString() ?? "Unknown";

                        string fullName = $"{manufacturer} {product}";
                        mbname.Text = WrapAfterWords(fullName, 5);
                    }
                }

                using (var searcher = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS"))
                {
                    foreach (var item in searcher.Get())
                    {
                        string biosVersion = item["SMBIOSBIOSVersion"]?.ToString() ?? "Unknown";

                        string biosDateRaw = item["ReleaseDate"]?.ToString() ?? "";
                        string biosDateFormatted = "Unknown";
                        if (!string.IsNullOrEmpty(biosDateRaw) && biosDateRaw.Length >= 8)
                        {
                            string year = biosDateRaw.Substring(0, 4);
                            string month = biosDateRaw.Substring(4, 2);
                            string day = biosDateRaw.Substring(6, 2);
                            biosDateFormatted = $"{day}.{month}.{year}";
                        }

                        biosver.Text = biosVersion;
                        biosdate.Text = biosDateFormatted;
                    }
                }
            }
            catch (Exception ex)
            {
                mbname.Text = $"{ex.Message}";
                biosver.Text = "N/A";
                biosdate.Text = "N/A";
            }
        }

        private void LoadStorageList()
        {
            try
            {
                _storageDevices = StorageHelper.GetAllStorageDevices()
                    .OrderByDescending(d => d.CapacityBytes)
                    .ToList();

                ssdComboBox.Items.Clear();
                if (_storageDevices.Count == 0)
                {
                    ssdnValue.Text = "N/A";
                    ssdcValue.Text = "N/A";
                    return;
                }

                for (int i = 0; i < _storageDevices.Count; i++)
                {
                    string displayName = !string.IsNullOrWhiteSpace(_storageDevices[i].Name)
                        ? _storageDevices[i].Name
                        : $"Drive #{i + 1}";
                    ssdComboBox.Items.Add($"{i + 1}. {displayName}");
                }

                ssdComboBox.SelectedIndex = 0;
                UpdateStorageInfo(0);
            }
            catch (Exception ex)
            {
                ssdnValue.Text = "N/A";
                ssdcValue.Text = "N/A";
            }
        }

        private void SSDComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ssdComboBox.SelectedIndex >= 0)
            {
                UpdateStorageInfo(ssdComboBox.SelectedIndex);
            }
        }

        private void UpdateStorageInfo(int index)
        {
            if (index < 0 || index >= _storageDevices.Count) return;

            var storage = _storageDevices[index];
            ssdnValue.Text = storage.Name;
            ssdcValue.Text = storage.CapacityFormatted;
        }

        private void LoadGpuList()
        {
            try
            {
                _gpus = GpuHelper.GetAllGpus()
                    .OrderByDescending(g => g.VRamBytes)
                    .ToList();

                videoComboBox.Items.Clear();
                if (_gpus.Count == 0)
                {
                    videon.Text = "N/A";
                    vram.Text = "N/A";
                    return;
                }

                for (int i = 0; i < _gpus.Count; i++)
                {
                    string displayName = !string.IsNullOrWhiteSpace(_gpus[i].Name)
                        ? _gpus[i].Name
                        : $"GPU #{i + 1}";
                    videoComboBox.Items.Add($"{i + 1}. {displayName}");
                }

                int maxIndex = _gpus
                    .Select((gpu, index) => new { gpu, index })
                    .OrderByDescending(x => x.gpu.VRamBytes)
                    .First().index;

                videoComboBox.SelectedIndex = maxIndex;
                UpdateGpuInfo(maxIndex);
            }
            catch (Exception ex)
            {
                videon.Text = "N/A";
                vram.Text = "N/A";
            }
        }

        private void VideoComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (videoComboBox.SelectedIndex >= 0)
            {
                UpdateGpuInfo(videoComboBox.SelectedIndex);
            }
        }

        private void UpdateGpuInfo(int index)
        {
            if (index < 0 || index >= _gpus.Count) return;

            var gpu = _gpus[index];
            videon.Text = gpu.Name;
            vram.Text = gpu.VRamFormatted;
        }

        private void LookResults_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://adderly.top/makubench",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Default Browser Error.", "MakuTweaker", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowComputerInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Manufacturer, Model FROM Win32_ComputerSystem");

                var item = searcher.Get().Cast<ManagementObject>().FirstOrDefault();

                string manufacturer = item?["Manufacturer"]?.ToString();
                string model = item?["Model"]?.ToString();

                string[] invalidModels = { "System Product Name", "To Be Filled By O.E.M." };

                bool invalid = string.IsNullOrWhiteSpace(manufacturer) ||
                               string.IsNullOrWhiteSpace(model) ||
                               manufacturer.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ||
                               model.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ||
                               invalidModels.Any(x => model.Equals(x, StringComparison.OrdinalIgnoreCase));

                if (!invalid)
                {
                    pcManufacturer.Text = manufacturer;
                    pcModel.Text = model;
                    computerSection.Visibility = Visibility.Visible;
                    labelcpu.Margin = new Thickness(0, 20, 0, 0);
                }
                else
                {
                    computerSection.Visibility = Visibility.Collapsed;
                    labelcpu.Margin = new Thickness(0, 0, 0, 0);
                }
            }
            catch
            {
                computerSection.Visibility = Visibility.Collapsed;
                labelcpu.Margin = new Thickness(0, 0, 0, 0);
            }
        }

        private void ShowSecurityInfo()
        {
            ShowTpm();
        }

        private void ShowTpm()
        {
            try
            {
                var scope = new ManagementScope(
                    @"\\.\root\cimv2\security\microsofttpm");
                scope.Connect();

                using var searcher = new ManagementObjectSearcher(
                    scope,
                    new ObjectQuery("SELECT IsEnabled_InitialValue FROM Win32_Tpm"));

                foreach (var item in searcher.Get())
                {
                    bool enabled = Convert.ToBoolean(item["IsEnabled_InitialValue"] ?? false);
                    ShowTpmStatus(enabled);
                }
            }
            catch
            {
                tpmStatus.Text = "N/A";
            }
        }

        private void LoadRamSticks()
        {
            try
            {
                var tempSticks = new List<RamStickInfo>();

                using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Capacity, Speed, PartNumber FROM Win32_PhysicalMemory"))
                using (var results = searcher.Get())
                {
                    foreach (var item in results)
                    {
                        tempSticks.Add(new RamStickInfo
                        {
                            Manufacturer = item["Manufacturer"]?.ToString()?.Trim() ?? "N/A",
                            CapacityBytes = Convert.ToUInt64(item["Capacity"] ?? 0),
                            Speed = Convert.ToInt32(item["Speed"] ?? 0),
                            PartNumber = item["PartNumber"]?.ToString()?.Trim() ?? "N/A"
                        });
                        item.Dispose();
                    }
                }

                Dispatcher.Invoke(() => {
                    _ramSticks = tempSticks;
                    ramStickComboBox.Items.Clear();
                    int i = 1;
                    foreach (var stick in _ramSticks)
                    {
                        ramStickComboBox.Items.Add($"{i}. {stick.CapacityFormatted} — {stick.Manufacturer}");
                        i++;
                    }

                    if (_ramSticks.Count > 0)
                    {
                        ramStickComboBox.SelectedIndex = 0;
                        UpdateRamStickInfo(0);
                    }
                });
            }
            catch
            {
                Dispatcher.Invoke(() => ramStickManufacturer.Text = "Error");
            }
        }

        private void UpdateRamStickInfo(int index)
        {
            if (index < 0 || index >= _ramSticks.Count) return;

            var stick = _ramSticks[index];

            ramStickManufacturer.Text = stick.Manufacturer;
            ramStickCapacity.Text = stick.CapacityFormatted;
            ramStickPart.Text = stick.PartNumber;
        }

        private void SaveDataToTxt()
        {
            try
            {
                var pci = MainWindow.Localization.LoadLocalization(
                    Properties.Settings.Default.lang ?? "en", "pci");

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "TXT File| *.txt",
                    Title = "MakuTweaker",
                    FileName = "MakuTweaker System Info.txt"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    StringBuilder sb = new StringBuilder();

                    sb.AppendLine("MakuTweaker // MarkAdderly");
                    sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    sb.AppendLine();
                    sb.AppendLine();

                    sb.AppendLine($"=== {pci["main"]["branding"]} ===");
                    sb.AppendLine($"{pci["main"]["manu"]} {pcManufacturer.Text}");
                    sb.AppendLine($"{pci["main"]["modeln"]} {pcModel.Text}");
                    sb.AppendLine();

                    sb.AppendLine($"=== {pci["main"]["processorlabel"]} ===");
                    sb.AppendLine($"{pci["main"]["processorname"]} {cpue.Text}");
                    sb.AppendLine($"{pci["main"]["processorcores"]} {cpucore.Text}");
                    sb.AppendLine($"{pci["main"]["processorthr"]} {threads.Text}");
                    sb.AppendLine($"{pci["main"]["processorfreq"]} {corespeed.Text}");
                    sb.AppendLine($"{pci["main"]["processorcache"]} {l3cash.Text}");
                    sb.AppendLine();

                    sb.AppendLine($"=== {pci["main"]["ramlabel"]} ===");
                    sb.AppendLine($"{pci["main"]["ramtotal"]} {rama.Text}");
                    sb.AppendLine($"{pci["main"]["ramddr"]} {ddre.Text}");
                    sb.AppendLine($"{pci["main"]["ramfreq"]} {freq.Text}");
                    sb.AppendLine();

                    sb.AppendLine($"=== {pci["main"]["mblabel"]} ===");
                    sb.AppendLine($"{pci["main"]["mbname"]} {mbname.Text}");
                    sb.AppendLine($"{pci["main"]["mbver"]} {biosver.Text}");
                    sb.AppendLine($"{pci["main"]["mbdate"]} {biosdate.Text}");
                    sb.AppendLine($"=== {pci["main"]["tpmtitle"]} ===");
                    sb.AppendLine($"{tpmStatus.Text}");
                    sb.AppendLine();
                    sb.AppendLine();
                    sb.AppendLine();

                    sb.AppendLine($"=== {pci["main"]["ramsticktitle"]} ===");

                    if (_ramSticks.Count == 0)
                    {
                        sb.AppendLine("N/A");
                    }
                    else
                    {
                        for (int i = 0; i < _ramSticks.Count; i++)
                        {
                            var stick = _ramSticks[i];

                            sb.AppendLine($"[{i + 1}]");
                            sb.AppendLine($"{pci["main"]["manu"]} {stick.Manufacturer}");
                            sb.AppendLine($"{pci["main"]["capac"]} {stick.CapacityFormatted}");
                            sb.AppendLine($"{pci["main"]["partnum"]} {stick.PartNumber}");
                            sb.AppendLine();
                        }
                    }

                    sb.AppendLine();
                    sb.AppendLine();
                    sb.AppendLine();

                    sb.AppendLine($"=== {pci["main"]["vlabel"]} ===");

                    if (_gpus.Count == 0)
                    {
                        sb.AppendLine("No GPU found");
                    }
                    else
                    {
                        for (int i = 0; i < _gpus.Count; i++)
                        {
                            var gpu = _gpus[i];

                            sb.AppendLine($"[{i + 1}] {gpu.Name}");
                            sb.AppendLine($"{pci["main"]["vmem"]} {gpu.VRamFormatted}");
                            sb.AppendLine();
                        }
                    }

                    sb.AppendLine();
                    sb.AppendLine();
                    sb.AppendLine();

                    sb.AppendLine($"=== {pci["main"]["ssdl"]} ===");

                    if (_storageDevices.Count == 0)
                    {
                        sb.AppendLine("N/A");
                    }
                    else
                    {
                        for (int i = 0; i < _storageDevices.Count; i++)
                        {
                            var storage = _storageDevices[i];

                            sb.AppendLine($"[{i + 1}] {storage.Name}");
                            sb.AppendLine($"{pci["main"]["smem"]} {storage.CapacityFormatted}");
                            sb.AppendLine();
                        }
                    }

                    sb.AppendLine();

                    File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);

                    MessageBox.Show(
                        "System information saved successfully!\nСистемная информация была успешно сохранена!",
                        "MakuTweaker",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ramStickComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ramStickComboBox.SelectedIndex >= 0)
                UpdateRamStickInfo(ramStickComboBox.SelectedIndex);
        }

        string WrapAfterWords(string text, int wordsPerLine = 5)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            var words = text.Split(' ');
            var lines = new List<string>();

            for (int i = 0; i < words.Length; i += wordsPerLine)
            {
                lines.Add(string.Join(" ", words.Skip(i).Take(wordsPerLine)));
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    public class GpuInfo
    {
        public string Name { get; set; } = string.Empty;
        public ulong VRamBytes { get; set; }
        public string VRamFormatted => FormatBytes(VRamBytes);
        public string LHMName { get; set; } = string.Empty;

        private static string FormatBytes(ulong bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
    public static class GpuHelper
    {
        public static List<GpuInfo> GetAllGpus()
        {
            var gpus = new List<GpuInfo>();

            try
            {
                using var factory = Vortice.DXGI.DXGI.CreateDXGIFactory1<Vortice.DXGI.IDXGIFactory1>();
                int i = 0;
                while (true)
                {
                    try
                    {
                        factory.EnumAdapters1((uint)i, out Vortice.DXGI.IDXGIAdapter1? adapter);
                        if (adapter == null)
                            break;

                        using (adapter)
                        {
                            var desc = adapter.Description1;
                            string name = desc.Description?.Trim() ?? "";

                            if (name.Contains("Microsoft Basic Render Driver", StringComparison.OrdinalIgnoreCase))
                            {
                                i++;
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(name) || name.Equals("Null", StringComparison.OrdinalIgnoreCase))
                            {
                                i++;
                                continue;
                            }

                            gpus.Add(new GpuInfo
                            {
                                Name = name,
                                VRamBytes = desc.DedicatedVideoMemory
                            });
                        }

                        i++;
                    }
                    catch (SharpGen.Runtime.SharpGenException)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                return FallbackToWmi();
            }

            return gpus.Count > 0 ? gpus : FallbackToWmi();
        }
        private static List<GpuInfo> FallbackToWmi()
        {
            var gpus = new List<GpuInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    string name = obj["Name"]?.ToString() ?? "Unknown GPU";
                    ulong vram = obj["AdapterRAM"] != null ? Convert.ToUInt64(obj["AdapterRAM"]) : 0;
                    gpus.Add(new GpuInfo { Name = name, VRamBytes = vram });
                }
            }
            catch { }
            return gpus;
        }
    }

    public class StorageInfo
    {
        public string Name { get; set; } = string.Empty;
        public ulong CapacityBytes { get; set; }
        public string CapacityFormatted => FormatBytes(CapacityBytes);
        public string Type { get; set; } = "";

        public string DevicePath { get; set; } = string.Empty;
        public ulong TotalBytesWritten { get; set; }
        public string TotalBytesWrittenFormatted => FormatBytes(TotalBytesWritten);

        private static string FormatBytes(ulong bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            if (bytes == 0)
            {
                return ("N/A");
            }
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    public class RamStickInfo
    {
        public string Manufacturer { get; set; } = "";
        public ulong CapacityBytes { get; set; }
        public int Speed { get; set; }
        public string PartNumber { get; set; } = "";

        public string CapacityFormatted =>
            $"{Math.Round(CapacityBytes / (1024.0 * 1024 * 1024), 1)} GB";
    }

    public static class StorageHelper
    {
        public static List<StorageInfo> GetAllStorageDevices()
        {
            var devices = new List<StorageInfo>();
            var diskWrites = new Dictionary<string, ulong>();
            try
            {
                var scope = new ManagementScope(@"\\.\root\microsoft\windows\storage");
                scope.Connect();

                using (var searcher = new ManagementObjectSearcher(
                    scope,
                    new ObjectQuery("SELECT DeviceId, TotalBytesWritten FROM MSFT_PhysicalDisk")))
                {
                    foreach (var obj in searcher.Get())
                    {
                        string deviceId = obj["DeviceId"]?.ToString() ?? string.Empty;
                        ulong bytesWritten = 0;

                        if (obj["TotalBytesWritten"] != null)
                        {
                            bytesWritten = Convert.ToUInt64(obj["TotalBytesWritten"]);
                        }

                        if (!string.IsNullOrEmpty(deviceId))
                        {
                            diskWrites[deviceId] = bytesWritten;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Caption, Size, DeviceID FROM Win32_DiskDrive");
                foreach (var obj in searcher.Get())
                {
                    string name = obj["Caption"]?.ToString() ?? "Unknown Device";

                    string type = "";
                    string lower = name.ToLower();
                    if (lower.Contains("nvme") || lower.Contains("pcie"))
                        type = "NVMe";

                    else if (lower.Contains("ssd") ||
                             lower.Contains("sata ssd") ||
                             lower.Contains("solid state"))
                        type = "SSD";

                    else if (lower.Contains("sd card") ||
                             lower.Contains("sdxc") ||
                             lower.Contains("sdhc") ||
                             lower.Contains("sd "))
                        type = "SD";

                    else if (lower.Contains("usb") ||
                             lower.Contains("flash") ||
                             lower.Contains("thumb"))
                        type = "USB";

                    else if (lower.Contains("hdd") ||
                             lower.Contains("hard drive") ||
                             lower.Contains("harddisk"))
                        type = "HDD";

                    ulong size = obj["Size"] != null ? Convert.ToUInt64(obj["Size"]) : 0;
                    string deviceID = obj["DeviceID"]?.ToString() ?? "";

                    if (size == 0 || name.Contains("Virtual", StringComparison.OrdinalIgnoreCase) || name.Contains("iSCSI", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string diskIndex = System.Text.RegularExpressions.Regex.Match(deviceID, @"\d+$").Value;

                    ulong totalWrites = 0;

                    if (!string.IsNullOrEmpty(diskIndex) && diskWrites.ContainsKey(diskIndex))
                    {
                        totalWrites = diskWrites[diskIndex];
                    }

                    string pathForCpp = deviceID.Replace("PHYSICALDRIVE", "PhysicalDrive", StringComparison.OrdinalIgnoreCase);

                    devices.Add(new StorageInfo
                    {
                        Name = name,
                        CapacityBytes = size,
                        DevicePath = pathForCpp,
                        TotalBytesWritten = totalWrites,
                        Type = type
                    });
                }
            }
            catch (Exception ex)
            {
            }
            return devices;
        }
    }
}