using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AlgowProforma.Models;

public partial class Reference : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString("N");
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _logoPath = "";

    public Reference Clone() => new()
    {
        Id = Id,
        Name = Name,
        LogoPath = LogoPath,
    };
}
