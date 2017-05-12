Use master
Go

CREATE DATABASE HelpDesk
Go

Use HelpDesk
Go

Create Table Category (
	CategoryID		UniqueIdentifier Not Null Default NewID(),
	Category		NVarChar(100) Not Null,
	CreatedBy		NVarChar(20) Not Null,
	CreationDate	DateTime Not Null Default GetDate(),
	ModifiedBy		NVarChar(20) Not Null,
	ModifiedDate	DateTime Not Null Default GetDate(),
	IsDeleted		Bit Not Null Default 0,

	Constraint PK_Category Primary Key (CategoryID Asc),
	Constraint UQ_Category_Category Unique (Category)
)
Go

Create Table Ticket (
	TicketID		UniqueIdentifier Not Null Default NewID(),
	TicketNo		NVarChar(10) Not Null,
	CategoryID		UniqueIdentifier Not Null,
	Subject			NVarChar(100) Not Null,
	Description		NText Not Null,
	Status			NChar(1),	-- N = New or Pending, I = In progress, T = Technician responded, A = Author responded, R =  Resolved, C = Closed successfully, U = Closed unsuccessfully, O = reopened
	CreatedBy		NVarChar(20) Not Null,
	CreationDate	DateTime Not Null Default GetDate(),
	ModifiedBy		NVarChar(20) Not Null,
	ModifiedDate	DateTime Not Null Default GetDate(),
	IsDeleted		Bit Not Null Default 0,

	Constraint PK_Ticket Primary Key (TicketID Asc),
	Constraint UQ_Ticket_TicketNo Unique (TicketNo),
	Constraint UQ_Ticket_CategoryID Foreign Key (CategoryID) References Category (CategoryID)
)
Go

Create Table TicketNote (
	TicketNoteID	UniqueIdentifier Not Null Default NewID(),
	TicketID		UniqueIdentifier Not Null,
	Note			NText Not Null,
	CreatedBy		NVarChar(20) Not Null,
	CreationDate	DateTime Not Null Default GetDate(),
	ModifiedBy		NVarChar(20) Not Null,
	ModifiedDate	DateTime Not Null Default GetDate(),
	IsDeleted		Bit Not Null Default 0,

	Constraint PK_TicketNote Primary Key (TicketNoteID Asc),
	Constraint FK_TicketNote_Ticket Foreign Key (TicketID) References Ticket (TicketID)
)
Go

Create Table TicketFile (
	TicketFileID	UniqueIdentifier Not Null Default NewID(),
	TicketID		UniqueIdentifier Not Null,
	FileName		NVarChar(100) Not Null,
	FileForTOrN		NChar(1) Not Null, -- Access only 2 values (T = Ticket, N = Note)
	RefID			UniqueIdentifier Not Null,	-- Can only be TicketID or TicketNoteID
	CreatedBy		NVarChar(20) Not Null,
	CreationDate	DateTime Not Null Default GetDate(),
	ModifiedBy		NVarChar(20) Not Null,
	ModifiedDate	DateTime Not Null Default GetDate(),
	IsDeleted		Bit Not Null Default 0,

	Constraint PK_TicketFile Primary Key (TicketFileID Asc),
	Constraint FK_TicketFile_Ticket Foreign Key (TicketID) References Ticket (TicketID),
	Constraint CK_TicketFile_FileForTOrN Check (FileForTOrN = 'T' Or FileForTOrN = 'N')
)
Go

Create Trigger tg_FileFile 
On TicketFile 
For Insert
As
Begin
	Declare @TicketFileID UniqueIdentifier, @RefID UniqueIdentifier, @FileForTOrN NChar(1), @ErrMsg NVarChar(100) = ''

	Select @TicketFileID = TicketFileID, @RefID = RefID, @FileForTOrN = FileForTOrN
	From Inserted

	If @FileForTOrN = 'T' And (Select Count(TicketID) From Ticket Where TicketID = @RefID) = 0
		Set @ErrMsg = 'Unknown ticket reference ID'

	If @FileForTOrN = 'N' And (Select Count(TicketNoteID) From TicketNote Where TicketNoteID = @RefID) = 0
		Set @ErrMsg = 'Unknown ticket note reference ID'

	If Len(LTrim(RTrim(@ErrMsg))) > 0
	Begin
		Delete From TicketFile Where TicketFileID = @TicketFileID
		Raiserror (@ErrMsg, 16, 1)
	End
End
Go

Create Table GeneralSetting (
	GeneralSettingID		UniqueIdentifier Not Null Default NewID(),
	EnableEmailAlert		Bit Not Null,
	EnableActionReminder	Bit Not Null,
	RecipientEmails			NText Not Null,
	ActionReminderInterval	Int Not Null,	-- In minutes

	Constraint PK_GeneralSetting Primary Key (GeneralSettingID Asc)
)	
Go


Insert Into GeneralSetting (EnableEmailAlert, EnableActionReminder, ActionReminderInterval, RecipientEmails)
Values (1, 1, 20, 'william.eboigbe@ieianchorpensions.com;akeem.aweda@ieianchorpensions.com')
Go

Insert Into Category (Category, CreatedBy, ModifiedBy)
Values ('Network Issues', 'AwedaA', 'AwedaA'),
		('Hardware Issues', 'AwedaA', 'AwedaA'),
		('Application Issues', 'AwedaA', 'AwedaA'),
		('Maintenance Issues', 'AwedaA', 'AwedaA'),
		('Change Request', 'AwedaA', 'AwedaA'),
		('License Issues', 'AwedaA', 'AwedaA'),
		('Enquiry', 'AwedaA', 'AwedaA'),
		('Other', 'AwedaA', 'AwedaA')