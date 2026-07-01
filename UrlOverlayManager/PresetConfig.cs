using System;
using System.Collections.Generic;

namespace UrlOverlayManager
{
    public class PresetConfig
    {
        public string Name { get; set; } = "";
        public List<OverlayItemConfig> Items { get; set; } = new List<OverlayItemConfig>();

        public static PresetConfig Create(string name, IEnumerable<OverlayItemConfig> sourceItems)
        {
            return new PresetConfig
            {
                Name = name,
                Items = CloneItems(sourceItems)
            };
        }

        public static List<OverlayItemConfig> CloneItems(IEnumerable<OverlayItemConfig> sourceItems)
        {
            List<OverlayItemConfig> result = new List<OverlayItemConfig>();

            foreach (OverlayItemConfig item in sourceItems)
            {
                result.Add(CloneItem(item, createNewId: true));
            }

            return result;
        }

        private static OverlayItemConfig CloneItem(OverlayItemConfig item, bool createNewId)
        {
            return new OverlayItemConfig
            {
                Id = createNewId ? Guid.NewGuid() : item.Id,
                Name = item.Name,
                Url = item.Url,
                Visible = item.Visible,
                ClickThrough = item.ClickThrough,
                Opacity = item.Opacity,
                X = item.X,
                Y = item.Y,
                Width = item.Width,
                Height = item.Height
            };
        }
    }
}
