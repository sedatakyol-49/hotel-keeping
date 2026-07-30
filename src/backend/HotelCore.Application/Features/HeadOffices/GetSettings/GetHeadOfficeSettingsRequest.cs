using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.HeadOffices.Common;

namespace HotelCore.Application.Features.HeadOffices.GetSettings;

/// <summary><c>GET /api/v1/head-office/settings</c> — kimlikteki Head Office.</summary>
public sealed record GetHeadOfficeSettingsRequest : IRequest<HeadOfficeSettingsResponse>;
