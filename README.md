# ⚔️ GameRealm API — ASP.NET Core 8 + JWT

## 🗂️ Structure
```
GameRealmAPI/
├── Controllers/
│   ├── AuthController.cs     ← Login, Register, Forgot/Change Password
│   ├── StatsController.cs    ← Home stats, Server stats, Online count
│   └── RanksController.cs    ← Top Players, Top Guilds
├── Data/
│   ├── AppDbContext.cs        ← Entity Framework setup
│   └── database_setup.sql    ← SQL script بديل للـ migrations
├── DTOs/
│   └── AuthDTOs.cs           ← كل الـ Request/Response objects
├── Helpers/
│   └── JwtHelper.cs          ← JWT token generation
├── Models/
│   ├── User.cs
│   ├── Player.cs
│   └── Guild.cs
├── Services/
│   ├── AuthService.cs        ← Business logic للـ Auth
│   ├── GameService.cs        ← Business logic للـ Stats & Ranks
│   └── EmailService.cs       ← MailKit للـ emails
├── appsettings.json
└── Program.cs                ← Middleware, DI, CORS
```

---

## 🚀 خطوات التشغيل

### 1. افتح `appsettings.json` وغير:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=GameRealmDB;Trusted_Connection=True;"
  },
  "Jwt": {
    "Key": "اكتب_هنا_كلمة_سر_طويلة_جداً_مش_اقل_من_32_حرف",
    "Issuer": "GameRealmAPI",
    "Audience": "GameRealmClient",
    "ExpiryInDays": 7
  },
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "Username": "your@gmail.com",
    "Password": "app_password_from_gmail"
  }
}
```

### 2. اعمل Database Migration:
```bash
# Install EF Tools (مرة واحدة بس)
dotnet tool install --global dotnet-ef

# اعمل Migration
dotnet ef migrations add InitialCreate

# طبّق على الـ Database
dotnet ef database update
```

**أو** لو عايز تشغل الـ SQL مباشرة:
```
افتح SQL Server Management Studio → شغّل ملف Data/database_setup.sql
```

### 3. شغّل الـ API:
```bash
dotnet run
```

### 4. افتح Swagger:
```
http://localhost:5000/swagger
```

---

## 🔌 ربط الـ Vue.js بالـ API

افتح `game-website/.env` وحط:
```
VITE_API_URL=http://localhost:5000/api
```

لما ترفع على السيرفر:
```
VITE_API_URL=https://api.yourgame.com/api
```

---

## 📋 الـ Endpoints كاملة

### Auth
| Method | URL | Description | Auth Required |
|--------|-----|-------------|---------------|
| POST | `/api/auth/login` | Login | ❌ |
| POST | `/api/auth/register` | Register | ❌ |
| POST | `/api/auth/forgot-password` | Send reset email | ❌ |
| POST | `/api/auth/change-password` | Change password | ✅ |
| POST | `/api/auth/reset-password` | Reset via token | ❌ |
| GET  | `/api/auth/me` | Get my info | ✅ |

### Stats
| Method | URL | Cache | Description |
|--------|-----|-------|-------------|
| GET | `/api/stats/home` | 5 min | Home page stats |
| GET | `/api/stats/server` | 2 min | Full server stats |
| GET | `/api/stats/online` | 1 min | Online player count |

### Ranks
| Method | URL | Cache | Description |
|--------|-----|-------|-------------|
| GET | `/api/ranks/players?limit=100&page=1` | 10 min | Top players |
| GET | `/api/ranks/guilds?limit=50&page=1` | 10 min | Top guilds |

---

## 🔒 Security Features
- ✅ BCrypt password hashing
- ✅ JWT tokens (7 days expiry)
- ✅ CORS limited to your frontend domain
- ✅ Server-side Output Cache
- ✅ Password reset token expires in 2 hours
- ✅ Ban & deactivation support
- ✅ Role-based authorization (Player, GM, Admin)
