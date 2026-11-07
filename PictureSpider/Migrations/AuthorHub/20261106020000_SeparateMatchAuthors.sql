-- Copy the matching dictionary without changing existing unified author identities.
-- MySQL DDL commits independently. Back up and verify before advancing each stage.
CREATE TABLE `MatchAuthors` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `Name` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin NOT NULL,
    `IsFallback` tinyint(1) NOT NULL DEFAULT 0,
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_bin;

INSERT INTO `MatchAuthors` (`Id`, `Name`, `IsFallback`)
SELECT a.`Id`, a.`Name`, a.`IsFallback` FROM `Authors` a
WHERE EXISTS (SELECT 1 FROM `AuthorAliases` b WHERE b.`AuthorId` = a.`Id`);

ALTER TABLE `AuthorAliases`
    DROP FOREIGN KEY `FK_AuthorAliases_Authors_AuthorId`,
    ADD CONSTRAINT `FK_AuthorAliases_MatchAuthors_AuthorId`
        FOREIGN KEY (`AuthorId`) REFERENCES `MatchAuthors` (`Id`) ON DELETE RESTRICT;

-- The approved candidate/source cleanup is performed separately in one transaction,
-- using exact backed-up identities, never an ID range or an LSF-only predicate.
-- Remove this matching-only column after that transaction has passed verification.
ALTER TABLE `Authors` DROP COLUMN `IsFallback`;
