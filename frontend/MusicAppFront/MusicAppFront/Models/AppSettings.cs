using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicAppFront.Models
{
    public class AppSettings
    {

        
        public string DlpServerUrlLog1 { get; set; } = string.Empty;
        public string DlpServerUrlLog2 { get; set; } = string.Empty;

        public string DlpServerUrlUnlog1 { get; set; } = string.Empty;
        public string DlpServerUrlUnlog2 { get; set; } = string.Empty;

        public string BaseAddress { get; set; } = string.Empty;

    }
}
