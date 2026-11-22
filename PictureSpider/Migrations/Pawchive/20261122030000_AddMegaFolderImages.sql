ALTER TABLE `Users` MODIFY COLUMN `dowloadExternalWorks`
    ENUM('None','ExternalVideos','KeyZipMega','GoogleDriveImageZip','MegaFolderImages') NOT NULL DEFAULT 'None';
