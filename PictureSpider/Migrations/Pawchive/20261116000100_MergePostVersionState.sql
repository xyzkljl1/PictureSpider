-- 执行前核验现有 postVersionState 全部为 Keep；保留现有 downloadStatus，不重判旧图组。
ALTER TABLE `pawchive`.`WorkGroups`
    MODIFY COLUMN `downloadStatus` enum('Pending','Skip','Downloading','Downloaded','Deleted') NOT NULL DEFAULT 'Pending',
    DROP COLUMN `postVersionState`;
