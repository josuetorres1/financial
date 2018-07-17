--
-- Copyright (c) Microsoft Corporation.  All rights reserved.
--

SET QUOTED_IDENTIFIER OFF 
GO
SET ANSI_NULLS ON 
GO

--
-- This must be ON to avoid trailing zeros in binary object.
--
SET ANSI_PADDING ON

USE [master]
GO

CREATE DATABASE [ASPStateInMemory]
ON PRIMARY (
	   NAME = ASPStateInMemory, FILENAME = 'D:\SQL\data\ASPStateInMemory_data.mdf'
),
FILEGROUP ASPStateInMemory_xtp_fg CONTAINS MEMORY_OPTIMIZED_DATA (
	   NAME = ASPStateInMemory_xtp, FILENAME = 'D:\SQL\data\ASPStateInMemory_xtp'
)
GO

USE [ASPStateInMemory]
GO

CREATE TABLE [ASPStateInMemory].dbo.[Sessions](
	SessionId nvarchar(88) COLLATE Latin1_General_100_BIN2 NOT NULL,
	Created datetime2 NOT NULL,
	Expires datetime2 NOT NULL,
	Initialized bit NOT NULL,
	Locked bit NOT NULL,
	LockDate datetime2 NOT NULL,
	LockCookie int NOT NULL,
	Timeout int NOT NULL,
	ItemSize bigint NOT NULL,
	Item varbinary(7000) NULL,

	-- The bucket count should be estimated on how many active sessions
	-- plus expired sessions are expected.
	--
	CONSTRAINT [PK_Sessions_SessionId]
		PRIMARY KEY NONCLUSTERED HASH (SessionId) WITH (BUCKET_COUNT = 1000000)
) 
WITH (MEMORY_OPTIMIZED=ON, DURABILITY=SCHEMA_ONLY)
GO

CREATE TYPE dbo.SessionIdTable AS TABLE
(
	-- The bucket count is based on how many expired sessions are expected
	-- to be found.
	--
	SessionId nvarchar(88) COLLATE Latin1_General_100_BIN2 NOT NULL PRIMARY KEY
		NONCLUSTERED HASH (SessionId) WITH (BUCKET_COUNT = 100000)
)
WITH (MEMORY_OPTIMIZED=ON)
GO

CREATE TABLE [ASPStateInMemory].dbo.SessionItems(
	-- Define [Id] only if the table has a DURABILITY=SCHEMA_AND_DATA.
	--
	-- Id bigint IDENTITY,

	SessionId nvarchar(88) COLLATE Latin1_General_100_BIN2 NOT NULL,
	SessionItemId int NOT NULL,
	Item varbinary(7000) NOT NULL,

	-- Define [PK_SessionItems_Id] only if the table has a DURABILITY=SCHEMA_AND_DATA.
	--
	-- The bucket count must be at lest two times [PK_Sessions_SessionId]
	-- since it will include at least two varbinary(7000) items.
	-- e.g., if the average session item is 32K, then it will fit in 5
	-- items in this table, therefore the size should be 5 times [PK_Sessions_SessionId].
	--
	-- CONSTRAINT [PK_SessionItems_Id] PRIMARY KEY NONCLUSTERED HASH (Id) WITH (BUCKET_COUNT = 1000000 * 2),

	-- The bucket count must be the same than [PK_Sessions_SessionId]
	INDEX [IX_SessionItems_SessionId]
		HASH (SessionId) WITH (BUCKET_COUNT = 1000000)
)
WITH (MEMORY_OPTIMIZED=ON, DURABILITY=SCHEMA_ONLY)
GO

CREATE TYPE dbo.SessionItemsTable AS TABLE
(
	-- The bucket count is based on how many chunks of varbinary(7000)
	-- are expected to be passed. E.g., 1000 times 7000 is 6MiB.
	--
	SessionItemId bigint NOT NULL PRIMARY KEY
		NONCLUSTERED HASH (SessionItemId) WITH (BUCKET_COUNT = 1000),
	Item varbinary(7000) NOT NULL
)
WITH (MEMORY_OPTIMIZED=ON)
GO

CREATE PROCEDURE dbo.InsertOrUpdateStateItem(
	@SessionId nvarchar(88) NOT NULL,
	@NewItem bit NOT NULL,
	@Initialized bit NOT NULL,
	@LockCookie int NOT NULL,
	@Timeout int NOT NULL,
	@ItemSize bigint NOT NULL,
	@Item varbinary(7000) NOT NULL
) WITH NATIVE_COMPILATION, SCHEMABINDING, EXECUTE AS OWNER
AS
BEGIN ATOMIC WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')

	DECLARE @Now AS datetime2 NOT NULL = GETUTCDATE()
	DECLARE @Expires AS datetime2 NOT NULL = DATEADD(minute, @Timeout, @Now)
			
	IF @NewItem = 1
	BEGIN
		INSERT dbo.Sessions
			(SessionId,
			Created,
			Expires,
			Initialized,
			Locked,
			LockDate,
			LockCookie,
			Timeout,
			ItemSize,
			Item)
		VALUES 
			(@SessionId,
			@Now,
			@Expires,
			@Initialized,
			0, -- Locked
			@Now, -- LockDate
			@LockCookie,
			@Timeout,
			@ItemSize,
			@Item)
		END
	ELSE
	BEGIN
		DECLARE @OldItemSize bigint = 0

		UPDATE dbo.Sessions
		SET
			@OldItemSize = ItemSize,
			Expires = @Expires,
			Locked = 0,
			Timeout = @Timeout,
			ItemSize = @ItemSize,
			Item = @Item
		WHERE SessionId = @SessionId AND LockCookie = @LockCookie

		IF @@ROWCOUNT = 0 RETURN;

		IF @OldItemSize > 7000
		BEGIN
			DELETE dbo.SessionItems WHERE SessionId = @SessionId
		END
	END
END 
GO

CREATE PROCEDURE dbo.InsertOrUpdateStateItemMedium(
	@SessionId nvarchar(88) NOT NULL,
	@NewItem bit NOT NULL,
	@Initialized bit NOT NULL,
	@LockCookie int NOT NULL,
	@Timeout int NOT NULL,
	@ItemSize bigint NOT NULL,
	@Item1 varbinary(7000) NOT NULL,
	@Item2 varbinary(7000) NOT NULL,
	@Item3 varbinary(7000),
	@Item4 varbinary(7000),
	@Item5 varbinary(7000),
	@Item6 varbinary(7000),
	@Item7 varbinary(7000),
	@Item8 varbinary(7000),
	@Item9 varbinary(7000)
) WITH NATIVE_COMPILATION, SCHEMABINDING, EXECUTE AS OWNER
AS
BEGIN ATOMIC WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')

	DECLARE @Now AS datetime2 NOT NULL = GETUTCDATE()
	DECLARE @Expires AS datetime2 NOT NULL = DATEADD(minute, @Timeout, @Now)
			
	IF @NewItem = 1
	BEGIN
		INSERT dbo.Sessions
			(SessionId,
			Created,
			Expires,
			Initialized,
			Locked,
			LockDate,
			LockCookie,
			Timeout,
			ItemSize,
			Item)
		VALUES 
			(@SessionId,
			@Now,
			@Expires,
			@Initialized,
			0, -- Locked
			@Now, -- LockDate
			@LockCookie,
			@Timeout,
			@ItemSize,
			NULL)
		END
	ELSE
	BEGIN
		DECLARE @OldItemSize bigint = 0

		UPDATE dbo.Sessions
		SET
			@OldItemSize = ItemSize,
			Expires = @Expires,
			Locked = 0,
			Timeout = @Timeout,
			ItemSize = @ItemSize,
			Item = NULL
		WHERE SessionId = @SessionId AND LockCookie = @LockCookie

		IF @@ROWCOUNT = 0 RETURN;

		IF @OldItemSize > 7000
		BEGIN
			DELETE dbo.SessionItems WHERE SessionId = @SessionId
		END
	END

	INSERT dbo.SessionItems(SessionId, SessionItemId, Item)
	VALUES (@SessionId, 1, @Item1)

	INSERT dbo.SessionItems(SessionId, SessionItemId, Item)
	VALUES (@SessionId, 2, @Item2)

	-- At least two items were expected.

	IF @Item3 IS NULL RETURN;

	INSERT dbo.SessionItems(SessionId, SessionItemId, Item)
	VALUES (@SessionId, 3, @Item3)

	IF @Item4 IS NULL RETURN;

	INSERT dbo.SessionItems(SessionId, SessionItemId, Item)
	VALUES (@SessionId, 4, @Item4)

	IF @Item5 IS NULL RETURN;

	INSERT dbo.SessionItems(SessionId, SessionItemId, Item)
	VALUES (@SessionId, 5, @Item5)

	IF @Item6 IS NULL RETURN;

	INSERT dbo.SessionItems(SessionId, SessionItemId, Item)
	VALUES (@SessionId, 6, @Item6)

	IF @Item7 IS NULL RETURN;

	INSERT dbo.SessionItems(SessionId, SessionItemId, Item)
	VALUES (@SessionId, 7, @Item7)

	IF @Item8 IS NULL RETURN;

	INSERT dbo.SessionItems(SessionId, SessionItemId, Item)
	VALUES (@SessionId, 8, @Item8)

	IF @Item9 IS NULL RETURN;

	INSERT dbo.SessionItems(SessionId, SessionItemId, Item)
	VALUES (@SessionId, 9, @Item9)
END 
GO

CREATE PROCEDURE dbo.InsertOrUpdateStateItemLarge(
	@SessionId nvarchar(88) NOT NULL,
	@NewItem bit NOT NULL,
	@LockCookie int NOT NULL,
	@Timeout int NOT NULL,
	@ItemSize bigint NOT NULL,
	@Items dbo.SessionItemsTable READONLY
) WITH NATIVE_COMPILATION, SCHEMABINDING, EXECUTE AS OWNER
AS
BEGIN ATOMIC WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')

	DECLARE @Now AS datetime2 NOT NULL = GETUTCDATE()
	DECLARE @Expires AS datetime2 NOT NULL = DATEADD(minute, @Timeout, @Now)
			
	IF @NewItem = 1
	BEGIN
		INSERT dbo.Sessions
			(SessionId,
			Created,
			Expires,
			Initialized,
			Locked, 
			LockDate,
			LockCookie,
			Timeout,
			ItemSize,
			Item)
		VALUES 
			(@SessionId,
			@Now,
			@Expires,
			1, -- Initialized
			0, -- Locked
			@Now, -- LockDate
			@LockCookie,
			@Timeout,
			@ItemSize,
			NULL)

		INSERT dbo.SessionItems(SessionId, SessionItemId, Item)
		SELECT @SessionId, SessionItemId, Item
		FROM @Items
	END
	ELSE
	BEGIN
		DECLARE @OldItemSize bigint = 0

		UPDATE dbo.Sessions
		SET
			@OldItemSize = ItemSize,
			Expires = @Expires,
			Locked = 0,
			Timeout = @Timeout,
			ItemSize = @ItemSize,
			Item = NULL
		WHERE SessionId = @SessionId AND LockCookie = @LockCookie

		IF @@ROWCOUNT = 0 RETURN;

		IF (@OldItemSize > 7000)
		BEGIN
			DELETE dbo.SessionItems WHERE SessionId = @SessionId
		END

		INSERT dbo.SessionItems(SessionId, SessionItemId, Item)
		SELECT @SessionId, SessionItemId, Item
		FROM @Items
	END
END
GO

CREATE PROCEDURE dbo.GetStateItem(
	@SessionId nvarchar(88) NOT NULL,
	@Locked bit OUTPUT,
	@LockAge int OUTPUT,
	@LockCookie int OUTPUT,
	@Initialized bit OUTPUT,
	@Item varbinary(7000) OUTPUT
) WITH NATIVE_COMPILATION, SCHEMABINDING, EXECUTE AS OWNER
AS
BEGIN ATOMIC WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')

	DECLARE @Now AS datetime2 NOT NULL = GETUTCDATE()
	DECLARE @ItemSize bigint NOT NULL = 0

	UPDATE dbo.Sessions
	SET
		Expires = DATEADD(minute, Timeout, @Now), 
		@Initialized = Initialized,
		Initialized = 1,
		@Locked = Locked,
		@LockAge = DATEDIFF(second, LockDate, @Now),
		@LockCookie = LockCookie,
		@ItemSize = ItemSize,
		@Item = Item
	WHERE SessionId = @SessionId

	IF @@ROWCOUNT = 0
	BEGIN
		SET @Locked = 0
		SET @LockAge = 0
		SET @LockCookie = 0
		SET @Initialized = 0
		SET @Item = NULL

		RETURN;
	END

	IF @Locked = 0
	BEGIN
		IF @ItemSize > 7000
		BEGIN
			-- Results have to be sorted by client.
			SELECT SessionItemId, Item
			FROM dbo.SessionItems
			WHERE SessionId = @SessionId
		END
	END
	ELSE
	BEGIN
		SET @Item = NULL
	END
END
GO

CREATE PROCEDURE dbo.GetStateItemExclusive(
	@SessionId nvarchar(88) NOT NULL,
	@Locked bit OUTPUT,
	@LockAge int OUTPUT,
	@LockCookie int OUTPUT,
	@Initialized bit OUTPUT,
	@Item varbinary(7000) OUTPUT
) WITH NATIVE_COMPILATION, SCHEMABINDING, EXECUTE AS OWNER
AS
BEGIN ATOMIC WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')

	DECLARE @Now AS datetime2 NOT NULL = GETUTCDATE()
	DECLARE @LockDate AS datetime2
	DECLARE @ItemSize bigint NOT NULL = 0

	UPDATE dbo.Sessions
	SET
		Expires = DATEADD(minute, Timeout, @Now), 
		@Initialized = Initialized,
		Initialized = 1,
		@Locked = Locked,
		Locked = 1,
		@LockDate = LockDate,
		@LockCookie = LockCookie,
		@ItemSize = ItemSize,
		@Item = Item
	WHERE SessionId = @SessionId

	IF @@ROWCOUNT = 0
	BEGIN
		SET @Locked = 0
		SET @LockAge = 0
		SET @LockCookie = 0
		SET @Initialized = 0
		SET @Item = NULL

		RETURN;
	END

	IF @Locked = 0
	BEGIN
		SET @LockCookie = @LockCookie + 1

		UPDATE dbo.Sessions
		SET LockDate = @Now,
			@LockAge = 0,
			LockCookie = @LockCookie
		WHERE SessionId = @SessionId

		IF @ItemSize > 7000
		BEGIN
			-- Results have to be sorted by client.
			SELECT SessionItemId, Item
			FROM dbo.SessionItems
			WHERE SessionId = @SessionId
		END
	END
	ELSE
	BEGIN
		SET @LockAge = DATEDIFF(second, @LockDate, @Now)
		SET @Item = NULL
	END
END
GO

CREATE PROCEDURE dbo.ReleaseStateItemExclusive(
	@SessionId nvarchar(88) NOT NULL,
	@LockCookie int NOT NULL
) WITH NATIVE_COMPILATION, SCHEMABINDING, EXECUTE AS OWNER
AS
BEGIN ATOMIC WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')

	DECLARE @Now AS datetime2 NOT NULL = GETUTCDATE()

	UPDATE dbo.Sessions
	SET
		Expires = DATEADD(minute, Timeout, @Now),
		Locked = 0
	WHERE SessionId = @SessionId AND LockCookie = @LockCookie
END
GO

CREATE PROCEDURE dbo.RemoveItem(
	@SessionId nvarchar(88) NOT NULL,
	@LockCookie int NOT NULL
) WITH NATIVE_COMPILATION, SCHEMABINDING, EXECUTE AS OWNER
AS
BEGIN ATOMIC WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')

	DELETE dbo.Sessions
	WHERE SessionId = @SessionId AND LockCookie = @LockCookie

	IF @@ROWCOUNT > 0
	BEGIN
		DELETE dbo.SessionItems
		WHERE SessionId = @SessionId
	END
END
GO

CREATE PROCEDURE dbo.ResetItemTimeout(
	@SessionId nvarchar(88) NOT NULL
) WITH NATIVE_COMPILATION, SCHEMABINDING, EXECUTE AS OWNER
AS
BEGIN ATOMIC WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')

	DECLARE @Now AS datetime2 NOT NULL = GETUTCDATE()

	UPDATE dbo.Sessions
	SET Expires = DATEADD(minute, Timeout, @Now)
	WHERE SessionId = @SessionId
END
GO

--
-- A job must be created to delete expired sessions.
--
CREATE PROCEDURE dbo.DeleteExpiredSessions
WITH NATIVE_COMPILATION, SCHEMABINDING, EXECUTE AS OWNER
AS
BEGIN ATOMIC WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')

	DECLARE @Now AS datetime2 NOT NULL = GETUTCDATE()
	DECLARE @SessionId AS nvarchar(88)
	DECLARE @ExpiredSessions AS dbo.SessionIdTable

	INSERT INTO @ExpiredSessions(SessionId)
	SELECT SessionId
	FROM dbo.Sessions
	WHERE Expires < @Now

	SELECT TOP 1
		@SessionId = SessionId
	FROM @ExpiredSessions

	WHILE @@ROWCOUNT > 0
	BEGIN
		DELETE dbo.Sessions WHERE SessionId = @SessionId
		DELETE dbo.SessionItems WHERE SessionId = @SessionId
		
		-- Get the next expired session.

		DELETE @ExpiredSessions WHERE SessionId = @SessionId

		SELECT TOP 1
			@SessionId = SessionId
		FROM @ExpiredSessions
	END
END
GO
