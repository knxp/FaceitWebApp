using Microsoft.JSInterop;
using System.Threading.Tasks;

namespace faceitWebApp.Services
{
    public interface IGoogleAnalyticsService
    {
        Task TrackPageView(string pagePath, string pageTitle);
        Task TrackEvent(string eventCategory, string eventAction, string eventLabel = null, int? eventValue = null);
    }

    public class GoogleAnalyticsService : IGoogleAnalyticsService
    {
        private readonly IJSRuntime _jsRuntime;

        public GoogleAnalyticsService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task TrackPageView(string pagePath, string pageTitle)
        {
            await _jsRuntime.InvokeVoidAsync("gtag", "config", "G-1R66FSQHBY", new { page_path = pagePath, page_title = pageTitle });
        }

        public async Task TrackEvent(string eventCategory, string eventAction, string eventLabel = null, int? eventValue = null)
        {
            await _jsRuntime.InvokeVoidAsync("gtag", "event", eventAction, new
            {
                event_category = eventCategory,
                event_label = eventLabel,
                value = eventValue
            });
        }
    }
}