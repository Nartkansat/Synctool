using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Synctool.Models;

namespace Synctool.Services
{
    public class CartService
    {
        private static CartService _instance;
        public static CartService Instance => _instance ??= new CartService();

        public ObservableCollection<CartItem> Items { get; } = new ObservableCollection<CartItem>();

        public event EventHandler CartChanged;

        private CartService() { }

        public void AddItem(CartItem item)
        {
            Items.Add(item);
            CartChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveItem(string id)
        {
            var item = Items.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                Items.Remove(item);
                CartChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Clear()
        {
            Items.Clear();
            CartChanged?.Invoke(this, EventArgs.Empty);
        }

        public int Count => Items.Count;
    }
}
