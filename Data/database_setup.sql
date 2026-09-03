-- =====================================================
-- GameRealm Database Setup
-- EntityID يبدأ من 1,000,000
-- الإيميل مش unique - ممكن حسابات كتير بنفس الإيميل
-- =====================================================

DROP TABLE IF EXISTS `Players`;
DROP TABLE IF EXISTS `Guilds`;
DROP TABLE IF EXISTS `Users`;
DROP TABLE IF EXISTS `__EFMigrationsHistory`;

-- USERS
CREATE TABLE `Users` (
    `EntityID`          INT NOT NULL AUTO_INCREMENT,
    `Username`          VARCHAR(50)  NOT NULL,
    `Email`             VARCHAR(100) NOT NULL,
    `PasswordHash`      VARCHAR(255) NOT NULL,
    `IsEmailVerified`   TINYINT(1)  NOT NULL DEFAULT 0,
    `IsActive`          TINYINT(1)  NOT NULL DEFAULT 1,
    `IsBanned`          TINYINT(1)  NOT NULL DEFAULT 0,
    `Role`              VARCHAR(20)  NOT NULL DEFAULT 'Player',
    `CreatedAt`         DATETIME(6)  NOT NULL DEFAULT NOW(6),
    `LastLoginAt`       DATETIME(6)  NULL,
    `ResetToken`        VARCHAR(100) NULL,
    `ResetTokenExpiry`  DATETIME(6)  NULL,
    CONSTRAINT `PK_Users` PRIMARY KEY (`EntityID`),
    UNIQUE INDEX `IX_Users_Username` (`Username`)
    -- لا يوجد unique على Email عشان يتقبل حسابات كتير بنفس الإيميل
) AUTO_INCREMENT=1000000 CHARACTER SET=utf8mb4;

-- GUILDS
CREATE TABLE `Guilds` (
    `EntityID`              INT NOT NULL AUTO_INCREMENT,
    `Name`                  VARCHAR(50)  NOT NULL,
    `Description`           LONGTEXT     NULL,
    `LeaderPlayerEntityID`  INT          NOT NULL DEFAULT 0,
    `Level`                 INT          NOT NULL DEFAULT 1,
    `Wins`                  INT          NOT NULL DEFAULT 0,
    `Losses`                INT          NOT NULL DEFAULT 0,
    `CreatedAt`             DATETIME(6)  NOT NULL DEFAULT NOW(6),
    CONSTRAINT `PK_Guilds` PRIMARY KEY (`EntityID`),
    UNIQUE INDEX `IX_Guilds_Name` (`Name`)
) AUTO_INCREMENT=1000000 CHARACTER SET=utf8mb4;

-- PLAYERS
CREATE TABLE `Players` (
    `EntityID`    INT          NOT NULL AUTO_INCREMENT,
    `UserId`      INT          NOT NULL,
    `Name`        VARCHAR(50)  NOT NULL,
    `Class`       VARCHAR(30)  NOT NULL DEFAULT 'Warrior',
    `Level`       INT          NOT NULL DEFAULT 1,
    `Experience`  BIGINT       NOT NULL DEFAULT 0,
    `Kills`       INT          NOT NULL DEFAULT 0,
    `Deaths`      INT          NOT NULL DEFAULT 0,
    `GuildId`     INT          NULL,
    `IsOnline`    TINYINT(1)   NOT NULL DEFAULT 0,
    `CreatedAt`   DATETIME(6)  NOT NULL DEFAULT NOW(6),
    `LastSeenAt`  DATETIME(6)  NOT NULL DEFAULT NOW(6),
    CONSTRAINT `PK_Players` PRIMARY KEY (`EntityID`),
    UNIQUE INDEX `IX_Players_UserId` (`UserId`),
    UNIQUE INDEX `IX_Players_Name` (`Name`),
    INDEX `IX_Players_GuildId` (`GuildId`),
    INDEX `IX_Players_Level_Kills` (`Level` DESC, `Kills` DESC),
    CONSTRAINT `FK_Players_Users` FOREIGN KEY (`UserId`) REFERENCES `Users`(`EntityID`) ON DELETE CASCADE,
    CONSTRAINT `FK_Players_Guilds` FOREIGN KEY (`GuildId`) REFERENCES `Guilds`(`EntityID`) ON DELETE SET NULL
) AUTO_INCREMENT=1000000 CHARACTER SET=utf8mb4;

-- EF Migrations History
CREATE TABLE `__EFMigrationsHistory` (
    `MigrationId`   VARCHAR(150) NOT NULL,
    `ProductVersion` VARCHAR(32) NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

-- WIKI COMMENTS
CREATE TABLE `WikiComments` (
    `Id`            INT NOT NULL AUTO_INCREMENT,
    `PageId`        INT NOT NULL,
    `ParentId`      INT NULL,
    `UserId`        INT NOT NULL,
    `AuthorName`    VARCHAR(100) NOT NULL,
    `Content`       LONGTEXT NOT NULL,
    `CreatedAt`     DATETIME(6) NOT NULL DEFAULT NOW(6),
    PRIMARY KEY (`Id`),
    INDEX `IX_WikiComments_PageId_CreatedAt` (`PageId`, `CreatedAt`),
    CONSTRAINT `FK_WikiComments_WikiPages` FOREIGN KEY (`PageId`) REFERENCES `WikiPages`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_WikiComments_Users` FOREIGN KEY (`UserId`) REFERENCES `Users`(`EntityID`) ON DELETE RESTRICT,
    CONSTRAINT `FK_WikiComments_Parent` FOREIGN KEY (`ParentId`) REFERENCES `WikiComments`(`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

-- WIKI REVIEWS
CREATE TABLE `WikiReviews` (
    `Id`            INT NOT NULL AUTO_INCREMENT,
    `PageId`        INT NOT NULL,
    `UserId`        INT NOT NULL,
    `AuthorName`    VARCHAR(100) NOT NULL,
    `Content`       LONGTEXT NOT NULL,
    `Rating`        INT NOT NULL,
    `CreatedAt`     DATETIME(6) NOT NULL DEFAULT NOW(6),
    PRIMARY KEY (`Id`),
    INDEX `IX_WikiReviews_PageId_CreatedAt` (`PageId`, `CreatedAt`),
    CONSTRAINT `FK_WikiReviews_WikiPages` FOREIGN KEY (`PageId`) REFERENCES `WikiPages`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_WikiReviews_Users` FOREIGN KEY (`UserId`) REFERENCES `Users`(`EntityID`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

SELECT 'Database setup complete! EntityID starts from 1,000,000' AS Status;
