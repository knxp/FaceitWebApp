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
        private readonly string _trackingId;

        public GoogleAnalyticsService(IJSRuntime jsRuntime, string trackingId)
        {
            _jsRuntime = jsRuntime;
            _trackingId = trackingId;
        }

        public async Task TrackPageView(string pagePath, string pageTitle)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("gtag", "config", _trackingId, new
                {
                    page_path = pagePath,
                    page_title = pageTitle
                });
            }
            catch (JSException ex)
            {
                Console.Error.WriteLine($"Failed to track page view: {ex.Message}");
            }
        }

        public async Task TrackEvent(string eventCategory, string eventAction, string eventLabel = null, int? eventValue = null)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("gtag", "event", eventAction, new
                {
                    event_category = eventCategory,
                    event_label = eventLabel,
                    value = eventValue
                });
            }
            catch (JSException ex)
            {
                Console.Error.WriteLine($"Failed to track event: {ex.Message}");
            }
        }
    }
}
