ALTER TABLE `pawchive`.`Users`
    ADD COLUMN `downloadAttachmentArchiveImages` tinyint(1) NOT NULL DEFAULT 0,
    ADD COLUMN `postVersionRule` enum('None','TitlePriority') NOT NULL DEFAULT 'None';
