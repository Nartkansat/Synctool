using System;
using System.Collections.Generic;
using System.Linq;

namespace Synctool.DTOs
{
    public class ManualCampaignInfoDto : System.ComponentModel.INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<ManualCampaignProductDto> Products { get; set; } = new();

        private bool _isExpanded = false;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                    OnPropertyChanged(nameof(VisibleProducts));
                    OnPropertyChanged(nameof(ExpandButtonText));
                    OnPropertyChanged(nameof(ExpandButtonIcon));
                }
            }
        }

        public bool HasMoreProducts => Products != null && Products.Count > 6;

        public List<ManualCampaignProductDto> VisibleProducts
        {
            get
            {
                if (Products == null) return new();
                if (IsExpanded || Products.Count <= 6) return Products;
                return Products.Take(6).ToList();
            }
        }

        public string ExpandButtonText => IsExpanded ? "Daha Az Göster" : $"Tüm Ürünleri Göster ({Products?.Count ?? 0})";
        public string ExpandButtonIcon => IsExpanded ? "ChevronUp" : "ChevronDown";

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }
}
