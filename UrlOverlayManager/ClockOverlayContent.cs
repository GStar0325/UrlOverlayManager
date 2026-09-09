using System;
using System.Net;

namespace UrlOverlayManager
{
    public static class ClockOverlayContent
    {
        public const string Url = "uom-clock://simple";

        public static bool IsClockUrl(string url)
        {
            return string.Equals(url.Trim(), Url, StringComparison.OrdinalIgnoreCase);
        }

        public static string CreateHtml()
        {
            string fontCss = CreateFontCss();
            string fontFamily = string.Equals(UiTheme.CurrentFontMode, UiTheme.SystemFontMode, StringComparison.OrdinalIgnoreCase)
                ? "Malgun Gothic"
                : "MabinogiClassic";

            return @"<!doctype html>
<html>
<head>
<meta charset=""utf-8"">
<style>
" + fontCss + @"
html, body {
    width: 100%;
    height: 100%;
    margin: 0;
    overflow: hidden;
    background: transparent;
}
body {
    display: flex;
    align-items: center;
    justify-content: center;
    color: white;
    font-family: '" + fontFamily + @"', sans-serif;
    text-shadow: 0 2px 4px rgba(0, 0, 0, 0.85), 0 0 14px rgba(0, 0, 0, 0.75);
}
#clock {
    width: 100%;
    box-sizing: border-box;
    padding: 4vh 5vw;
    text-align: center;
    line-height: 1;
    white-space: nowrap;
}
#period {
    display: inline-block;
    margin-right: 0.24em;
    font-size: clamp(18px, 10vmin, 64px);
    vertical-align: baseline;
}
#time {
    font-size: clamp(32px, 25vmin, 170px);
}
#seconds {
    display: inline-block;
    margin-left: 0.18em;
    font-size: clamp(16px, 9vmin, 56px);
    vertical-align: baseline;
}
</style>
</head>
<body>
<div id=""clock""><span id=""period""></span><span id=""time""></span><span id=""seconds""></span></div>
<script>
const period = document.getElementById('period');
const time = document.getElementById('time');
const secondsElement = document.getElementById('seconds');

function updateClock() {
    const now = new Date();
    const hour24 = now.getHours();
    const hour12 = hour24 % 12 || 12;
    period.textContent = hour24 < 12 ? '오전' : '오후';
    const hours = String(hour12).padStart(2, '0');
    const minutes = String(now.getMinutes()).padStart(2, '0');
    const seconds = String(now.getSeconds()).padStart(2, '0');
    time.textContent = `${hours}:${minutes}`;
    secondsElement.textContent = seconds;
}

updateClock();
setInterval(updateClock, 250);
</script>
</body>
</html>";
        }

        private static string CreateFontCss()
        {
            if (string.Equals(UiTheme.CurrentFontMode, UiTheme.SystemFontMode, StringComparison.OrdinalIgnoreCase))
                return "";

            byte[]? fontDataBytes = UiTheme.ReadAppFontData();

            if (fontDataBytes == null)
                return "";

            string fontData = Convert.ToBase64String(fontDataBytes);

            return "@font-face { font-family: 'MabinogiClassic'; src: url('data:font/ttf;base64,"
                + WebUtility.HtmlEncode(fontData)
                + "') format('truetype'); font-weight: 400; font-style: normal; }";
        }
    }
}
