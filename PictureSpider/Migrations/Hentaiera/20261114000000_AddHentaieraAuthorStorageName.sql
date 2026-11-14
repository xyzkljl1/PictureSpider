ALTER TABLE `Hentaiera`.`Users`
    ADD COLUMN `AuthorStorageName` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci NULL DEFAULT NULL,
    ADD INDEX `IX_Users_AuthorStorageName` (`AuthorStorageName`),
    ADD CONSTRAINT `FK_Users_AuthorHub_StorageName` FOREIGN KEY (`AuthorStorageName`)
        REFERENCES `authorhub`.`Authors` (`StorageName`) ON DELETE RESTRICT ON UPDATE RESTRICT;
