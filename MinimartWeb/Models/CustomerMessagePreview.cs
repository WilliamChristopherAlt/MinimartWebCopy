
using MinimartWeb.Model;


namespace MinimartWeb.Model
{

    public class CustomerMessagePreview
    {
        public Customer Customer { get; set; }
        public byte[]? LatestMessage { get; set; }
        public DateTime? LatestTime { get; set; }
    }
}
