-- --------------------------------------------------------
-- 主机:                           127.0.0.1
-- 服务器版本:                        8.0.31 - MySQL Community Server - GPL
-- 服务器OS:                        Win64
-- HeidiSQL 版本:                  10.2.0.5599
-- --------------------------------------------------------

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET NAMES utf8 */;
/*!50503 SET NAMES utf8mb4 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;


-- Dumping database structure for pass
CREATE DATABASE IF NOT EXISTS `pass` /*!40100 DEFAULT CHARACTER SET utf8mb3 COLLATE utf8mb3_bin */ /*!80016 DEFAULT ENCRYPTION='N' */;
USE `pass`;

-- Dumping structure for table pass.illust
CREATE TABLE IF NOT EXISTS `illust` (
  `id` int NOT NULL DEFAULT '0',
  `title` varchar(500) NOT NULL DEFAULT 'ERROR',
  `description` varchar(6000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL DEFAULT '0',
  `xRestrict` int NOT NULL DEFAULT '0',
  `tags` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL DEFAULT '',
  `userId` int NOT NULL DEFAULT '0',
  `width` int unsigned NOT NULL DEFAULT '0',
  `height` int unsigned NOT NULL DEFAULT '0',
  `pageCount` int unsigned NOT NULL DEFAULT '1',
  `bookmarked` tinyint(1) NOT NULL DEFAULT '0',
  `bookmarkPrivate` tinyint(1) NOT NULL DEFAULT '0',
  `urlFormat` varchar(300) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL DEFAULT 'ERROR',
  `urlThumbFormat` varchar(300) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL DEFAULT 'ERROR',
  `readed` tinyint(1) NOT NULL DEFAULT '0',
  `bookmarkEach` varchar(200) NOT NULL DEFAULT '',
  `valid` int NOT NULL DEFAULT '1',
  `likeCount` int NOT NULL DEFAULT '0',
  `bookmarkCount` int NOT NULL DEFAULT '0',
  `updateTime` timestamp NOT NULL DEFAULT '2000-01-01 00:00:00',
  `ugoiraFrames` text,
  `ugoiraURL` varchar(300) NOT NULL DEFAULT '',
  `viewCount` int NOT NULL DEFAULT '0',
  `uploadDate` timestamp NOT NULL DEFAULT '2000-01-01 00:00:00',
  PRIMARY KEY (`id`),
  KEY `FK_illust_user` (`userId`),
  CONSTRAINT `FK_illust_user` FOREIGN KEY (`userId`) REFERENCES `user` (`userId`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Data exporting was unselected.

-- Dumping structure for table pass.invalidkeyword
CREATE TABLE IF NOT EXISTS `invalidkeyword` (
  `word` varchar(100) COLLATE utf8mb3_bin NOT NULL,
  PRIMARY KEY (`word`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_bin;

-- Data exporting was unselected.

-- Dumping structure for table pass.keyword
CREATE TABLE IF NOT EXISTS `keyword` (
  `word` varchar(100) COLLATE utf8mb3_bin NOT NULL,
  `type` enum('tag','word') COLLATE utf8mb3_bin NOT NULL DEFAULT 'tag',
  `status` enum('Follow','Ignore','None') CHARACTER SET utf8mb3 COLLATE utf8mb3_bin NOT NULL DEFAULT 'None',
  `desc` varchar(50) COLLATE utf8mb3_bin NOT NULL DEFAULT '',
  PRIMARY KEY (`word`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_bin;

-- Data exporting was unselected.

-- Dumping structure for table pass.status
CREATE TABLE IF NOT EXISTS `status` (
  `Id` varchar(50) COLLATE utf8mb3_bin NOT NULL DEFAULT '0',
  `CookieCache` text COLLATE utf8mb3_bin NOT NULL,
  `QueueUpdateTime` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `Queue` mediumtext COLLATE utf8mb3_bin NOT NULL,
  `CSRFTokenCache` varchar(300) CHARACTER SET utf8mb3 COLLATE utf8mb3_bin NOT NULL DEFAULT '""',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_bin;

-- Data exporting was unselected.

-- Dumping structure for table pass.user
CREATE TABLE IF NOT EXISTS `user` (
  `userId` int NOT NULL DEFAULT '0',
  `userName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL DEFAULT '',
  `followed` tinyint(1) NOT NULL DEFAULT '0',
  `queued` tinyint(1) NOT NULL DEFAULT '0',
  `updateTime` timestamp NOT NULL DEFAULT '2000-01-01 00:00:00',
  PRIMARY KEY (`userId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Data exporting was unselected.

/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IF(@OLD_FOREIGN_KEY_CHECKS IS NULL, 1, @OLD_FOREIGN_KEY_CHECKS) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
