IF NOT EXISTS(SELECT 1 FROM dbo.tblImageTypes WHERE ImageTypeName=N'کارت بایگانی')
    INSERT INTO dbo.tblImageTypes(ImageTypeName,IsActive) VALUES(N'کارت بایگانی',1);
GO
