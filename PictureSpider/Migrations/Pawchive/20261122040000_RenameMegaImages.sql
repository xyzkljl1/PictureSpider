ALTER TABLE `pawchive`.`Users` MODIFY COLUMN `dowloadExternalWorks`
    ENUM('None','ExternalVideos','KeyZipMega','GoogleDriveImageZip','MegaFolderImages','MegaImages') NOT NULL DEFAULT 'None';

UPDATE `pawchive`.`Users` SET `dowloadExternalWorks` = 'MegaImages'
WHERE `dowloadExternalWorks` = 'MegaFolderImages';

ALTER TABLE `pawchive`.`Users` MODIFY COLUMN `dowloadExternalWorks`
    ENUM('None','ExternalVideos','KeyZipMega','GoogleDriveImageZip','MegaImages') NOT NULL DEFAULT 'None';
