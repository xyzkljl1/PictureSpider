ALTER TABLE `pawchive`.`Users` MODIFY COLUMN `dowloadExternalWorks`
    ENUM('None','DirectExternal','KeyZipMega','GoogleDriveImageZip','ExternalVideos') NOT NULL DEFAULT 'None';

UPDATE `pawchive`.`Users` SET `dowloadExternalWorks` = 'ExternalVideos'
WHERE `dowloadExternalWorks` = 'DirectExternal';

ALTER TABLE `pawchive`.`Users` MODIFY COLUMN `dowloadExternalWorks`
    ENUM('None','ExternalVideos','KeyZipMega','GoogleDriveImageZip') NOT NULL DEFAULT 'None';
