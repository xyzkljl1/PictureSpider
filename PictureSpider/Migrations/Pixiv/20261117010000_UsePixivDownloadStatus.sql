ALTER TABLE illust
    ADD COLUMN downloadStatus ENUM('Downloading','Downloaded') NOT NULL DEFAULT 'Downloading';

UPDATE illust SET downloadStatus = 'Downloaded' WHERE downloaded = TRUE;

ALTER TABLE illust DROP COLUMN downloaded;
