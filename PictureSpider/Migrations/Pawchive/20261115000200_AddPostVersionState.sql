ALTER TABLE `pawchive`.`WorkGroups`
    ADD COLUMN `postVersionState` enum('Keep','Pending','Skip') NOT NULL DEFAULT 'Keep';
