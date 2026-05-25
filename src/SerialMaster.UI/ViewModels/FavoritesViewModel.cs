using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialMaster.UI.Models;
using SerialMaster.UI.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace SerialMaster.UI.ViewModels;

public partial class FavoritesViewModel : ObservableObject
{
    private readonly FavoritesService _favoritesService;
    private readonly Func<byte[], Task> _sendCallback;
    private readonly Action _closeCallback;

    [ObservableProperty]
    private ObservableCollection<FavoriteItem> _items = new();

    [ObservableProperty]
    private FavoriteItem? _selectedItem;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string _editData = string.Empty;

    [ObservableProperty]
    private string _editCategory = "默认";

    [ObservableProperty]
    private string _tabTitle = "⭐ 发送收藏夹";

    public FavoritesViewModel(FavoritesService favoritesService, Func<byte[], Task> sendCallback, Action closeCallback)
    {
        _favoritesService = favoritesService;
        _sendCallback = sendCallback;
        _closeCallback = closeCallback;
        Load();
    }

    private void Load()
    {
        var list = _favoritesService.Load();
        Items = new ObservableCollection<FavoriteItem>(list);
    }

    private void Save() => _favoritesService.Save(Items.ToList());

    public void Close() => _closeCallback();

    [RelayCommand]
    private void Add()
    {
        if (string.IsNullOrWhiteSpace(EditName) || string.IsNullOrWhiteSpace(EditData))
        {
            MessageBox.Show("名称和数据不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var item = new FavoriteItem
        {
            Name = EditName.Trim(),
            Data = EditData.Trim(),
            Category = string.IsNullOrWhiteSpace(EditCategory) ? "默认" : EditCategory.Trim(),
            IsHex = true
        };
        Items.Add(item);
        Save();
    }

    [RelayCommand]
    private void Delete(FavoriteItem? item)
    {
        if (item == null) return;
        Items.Remove(item);
        if (SelectedItem == item) SelectedItem = null;
        Save();
    }

    [RelayCommand]
    private async Task Send(FavoriteItem? item)
    {
        if (item == null) return;
        try
        {
            byte[] data;
            string text = item.Data.Replace(" ", "");
            if (item.IsHex)
            {
                if (text.Length % 2 != 0) text = "0" + text;
                data = new byte[text.Length / 2];
                for (int i = 0; i < data.Length; i++)
                    data[i] = Convert.ToByte(text.Substring(i * 2, 2), 16);
            }
            else
            {
                data = System.Text.Encoding.UTF8.GetBytes(item.Data);
            }
            await _sendCallback(data);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"发送失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        if (MessageBox.Show("确定要清空所有收藏吗?", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            Items.Clear();
            Save();
        }
    }
}
