using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace RelatorioGLPIApp.Models
{
    public partial class TechnicianDetailTicket : ObservableObject
    {
        public int Id { get; init; }
        public string Title { get; init; }
        public string Type { get; init; }

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private bool _isLoadingConversation;

        [ObservableProperty]
        private ObservableCollection<TicketFollowup> _conversation = new();

        public TechnicianDetailTicket(int id, string title, string type)
        {
            Id = id;
            Title = title;
            Type = type;
        }
    }
}