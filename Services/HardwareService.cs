
using System;
using System.Management;

namespace OpenClaw.Windows.Services;

public class HardwareService
{
    public double GetTotalVramGb()
    {
        try
        {
            // Windows-specific way to get Video RAM
            // Win32_VideoController usually returns AdapterRAM in bytes.
            // Note: This often creates duplicates for integrated + dedicated graphics.
            // We want the MAX value found to represent the dedicated card.
            
            double maxVramBytes = 0;

            using (var searcher = new ManagementObjectSearcher("SELECT AdapterRAM FROM Win32_VideoController"))
            {
                foreach (ManagementObject queryObj in searcher.Get())
                {
                    try 
                    {
                        var ram = Convert.ToDouble(queryObj["AdapterRAM"]);
                        if (ram > maxVramBytes) maxVramBytes = ram;
                    }
                    catch { }
                }
            }
            
            // If Win32_VideoController fails (returns 0 or null, common on some drivers),
            // it's hard to get without DirectX. We'll fallback to a safe assumption or 0.
            
            if (maxVramBytes <= 0) return 0;

            return Math.Round(maxVramBytes / 1024 / 1024 / 1024, 1);
        }
        catch (Exception)
        {
            return 0; // Failure to detect
        }
    }
}
