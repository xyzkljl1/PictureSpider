ALTER TABLE `AuthorSources`
    MODIFY COLUMN `Module` enum('Pixiv','Twitter','Hitomi','Kemono','Pawchive','LSF')
    CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin NOT NULL;

CREATE TRIGGER `Authors_InsertLsfSource`
AFTER INSERT ON `Authors`
FOR EACH ROW
    INSERT INTO `AuthorSources` (`Module`, `SourceKey`, `AuthorId`)
    VALUES ('LSF', NEW.`StorageName`, NEW.`Id`);

INSERT INTO `AuthorSources` (`Module`, `SourceKey`, `AuthorId`)
SELECT 'LSF', a.`StorageName`, a.`Id`
FROM `Authors` a
WHERE NOT EXISTS (
    SELECT 1 FROM `AuthorSources` s
    WHERE s.`Module` = 'LSF' AND s.`SourceKey` = a.`StorageName`);
