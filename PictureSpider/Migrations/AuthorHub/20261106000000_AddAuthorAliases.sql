CREATE TABLE `AuthorAliases` (
    `AuthorId` bigint NOT NULL,
    `Name` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin NOT NULL,
    `AllowAutoMatch` tinyint(1) NOT NULL DEFAULT 0,
    PRIMARY KEY (`AuthorId`, `Name`),
    CONSTRAINT `FK_AuthorAliases_Authors_AuthorId`
        FOREIGN KEY (`AuthorId`) REFERENCES `Authors` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_bin;
