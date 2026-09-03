-- =====================================================
-- Migration: Add Store Tables
-- Run this in Navicat after running Update-Database
-- =====================================================

-- Store Items
CREATE TABLE IF NOT EXISTS `StoreItems` (
    `Id`          INT NOT NULL AUTO_INCREMENT,
    `Name`        VARCHAR(100) NOT NULL,
    `Description` LONGTEXT NOT NULL,
    `Icon`        VARCHAR(10) NOT NULL DEFAULT '🎁',
    `Category`    VARCHAR(30) NOT NULL DEFAULT 'general',
    `PriceCoins`  INT NOT NULL DEFAULT 0,
    `Badge`       VARCHAR(30) NULL,
    `BadgeType`   VARCHAR(20) NULL,
    `IsActive`    TINYINT(1) NOT NULL DEFAULT 1,
    `Stock`       INT NOT NULL DEFAULT -1,
    `CreatedAt`   DATETIME(6) NOT NULL DEFAULT NOW(6),
    PRIMARY KEY (`Id`)
) AUTO_INCREMENT=1 CHARACTER SET=utf8mb4;

-- Coin Packages
CREATE TABLE IF NOT EXISTS `CoinPackages` (
    `Id`           INT NOT NULL AUTO_INCREMENT,
    `Coins`        INT NOT NULL,
    `Price`        DECIMAL(10,2) NOT NULL,
    `BonusPercent` INT NOT NULL DEFAULT 0,
    `IsPopular`    TINYINT(1) NOT NULL DEFAULT 0,
    `IsActive`     TINYINT(1) NOT NULL DEFAULT 1,
    PRIMARY KEY (`Id`)
) AUTO_INCREMENT=1 CHARACTER SET=utf8mb4;

-- Player Wallets
CREATE TABLE IF NOT EXISTS `PlayerWallets` (
    `Id`        INT NOT NULL AUTO_INCREMENT,
    `UserId`    INT NOT NULL,
    `Coins`     INT NOT NULL DEFAULT 0,
    `UpdatedAt` DATETIME(6) NOT NULL DEFAULT NOW(6),
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `IX_PlayerWallets_UserId` (`UserId`),
    CONSTRAINT `FK_PlayerWallets_Users` FOREIGN KEY (`UserId`) REFERENCES `Users`(`EntityID`) ON DELETE CASCADE
) AUTO_INCREMENT=1 CHARACTER SET=utf8mb4;

-- Purchase Logs
CREATE TABLE IF NOT EXISTS `PurchaseLogs` (
    `Id`             INT NOT NULL AUTO_INCREMENT,
    `UserId`         INT NOT NULL,
    `StoreItemId`    INT NULL,
    `CoinPackageId`  INT NULL,
    `Type`           VARCHAR(20) NOT NULL DEFAULT 'item',
    `CoinsSpent`     INT NOT NULL DEFAULT 0,
    `CoinsReceived`  INT NOT NULL DEFAULT 0,
    `Status`         VARCHAR(20) NOT NULL DEFAULT 'completed',
    `CreatedAt`      DATETIME(6) NOT NULL DEFAULT NOW(6),
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_PurchaseLogs_Users` FOREIGN KEY (`UserId`) REFERENCES `Users`(`EntityID`) ON DELETE CASCADE,
    CONSTRAINT `FK_PurchaseLogs_StoreItems` FOREIGN KEY (`StoreItemId`) REFERENCES `StoreItems`(`Id`) ON DELETE SET NULL
) AUTO_INCREMENT=1 CHARACTER SET=utf8mb4;

-- Payments (PayPal/Card/Arcen - for future use)
CREATE TABLE IF NOT EXISTS `Payments` (
    `Id`                    INT NOT NULL AUTO_INCREMENT,
    `UserId`                INT NOT NULL,
    `CoinPackageId`         INT NOT NULL,
    `Method`                VARCHAR(20) NOT NULL,
    `Amount`                DECIMAL(10,2) NOT NULL,
    `Currency`              VARCHAR(5) NOT NULL DEFAULT 'USD',
    `Status`                VARCHAR(20) NOT NULL DEFAULT 'pending',
    `ExternalTransactionId` VARCHAR(255) NULL,
    `CreatedAt`             DATETIME(6) NOT NULL DEFAULT NOW(6),
    `CompletedAt`           DATETIME(6) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Payments_Users` FOREIGN KEY (`UserId`) REFERENCES `Users`(`EntityID`) ON DELETE CASCADE,
    CONSTRAINT `FK_Payments_CoinPackages` FOREIGN KEY (`CoinPackageId`) REFERENCES `CoinPackages`(`Id`) ON DELETE RESTRICT
) AUTO_INCREMENT=1 CHARACTER SET=utf8mb4;

-- Seed default coin packages
INSERT IGNORE INTO `CoinPackages` (`Id`, `Coins`, `Price`, `BonusPercent`, `IsPopular`) VALUES
(1, 100,  10.00, 0,  0),
(2, 300,  25.00, 20, 0),
(3, 600,  50.00, 40, 1),
(4, 1500, 100.00, 50, 0);

SELECT 'Store tables created successfully!' AS Status;
