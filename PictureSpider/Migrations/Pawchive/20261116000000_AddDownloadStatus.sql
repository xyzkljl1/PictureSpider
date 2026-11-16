ALTER TABLE `pawchive`.`WorkGroups`
    ADD COLUMN `downloadStatus` enum('Skip','Downloading','Downloaded','Deleted') NOT NULL DEFAULT 'Downloading';
