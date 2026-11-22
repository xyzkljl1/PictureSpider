-- 停用整数映射的旧版本后执行。保留原主键，直到新列按名称完成转换。
ALTER TABLE `pawchive`.`ExternalWorks`
    ADD COLUMN `typeName` ENUM('Mega','GoogleDrive','MegaFolderReference') NULL AFTER `type`;

UPDATE `pawchive`.`ExternalWorks`
SET `typeName` = CASE `type`
    WHEN 0 THEN 'Mega'
    WHEN 1 THEN 'GoogleDrive'
    WHEN 2 THEN 'MegaFolderReference'
END;

ALTER TABLE `pawchive`.`ExternalWorks`
    DROP PRIMARY KEY,
    DROP COLUMN `type`,
    CHANGE COLUMN `typeName` `type` ENUM('Mega','GoogleDrive','MegaFolderReference') NOT NULL,
    ADD PRIMARY KEY (`id`, `type`);
