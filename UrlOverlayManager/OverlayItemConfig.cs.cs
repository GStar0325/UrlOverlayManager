using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrlOverlayManager
{
    public class OverlayItemConfig
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = "";
        public string Url { get; set; } = "";

        public bool Visible { get; set; } = true;
        public bool ClickThrough { get; set; } = false;

        public double Opacity { get; set; } = 0.9;
        public int X { get; set; } = 100;
        public int Y { get; set; } = 100;
        public int Width { get; set; } = 400;
        public int Height { get; set; } = 600;
    }
}
