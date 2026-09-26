param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
$output = if($OutputDirectory){$OutputDirectory}else{Join-Path $PSScriptRoot 'concepts'}
New-Item -ItemType Directory -Path $output -Force | Out-Null
$styles = @'
<Grid.Resources>
 <Style TargetType="TextBlock"><Setter Property="TextWrapping" Value="Wrap"/><Setter Property="Foreground" Value="#E9F0EC"/><Setter Property="FontFamily" Value="Segoe UI"/><Setter Property="FontSize" Value="14"/></Style>
 <Style x:Key="muted" TargetType="TextBlock" BasedOn="{StaticResource {x:Type TextBlock}}"><Setter Property="Foreground" Value="#A4B4AD"/><Setter Property="FontSize" Value="12"/></Style>
 <Style x:Key="action" TargetType="Border"><Setter Property="Background" Value="#167C59"/><Setter Property="CornerRadius" Value="7"/><Setter Property="Padding" Value="18,11"/></Style>
</Grid.Resources>
'@
$a = @'
<Grid.ColumnDefinitions><ColumnDefinition Width="200"/><ColumnDefinition/></Grid.ColumnDefinitions>
<Border Background="#0D1713" BorderBrush="#26362F" BorderThickness="0,0,1,0" Padding="20,28"><DockPanel>
 <StackPanel DockPanel.Dock="Top"><TextBlock Text="A /" Foreground="#8BD9B4" FontSize="32" FontWeight="Light"/><TextBlock Text="ARTO" FontSize="21" FontWeight="SemiBold" Margin="0,12,0,0"/><TextBlock Text="SALES COPILOT" Style="{StaticResource muted}" Margin="0,4,0,48"/>
 <Border Background="#223A2E" CornerRadius="7" Padding="14,12"><TextBlock Text="◉   Call room" Foreground="#A9E6C8" FontWeight="SemiBold"/></Border>
 <TextBlock Text="▤   Preparation" Margin="14,22,0,0"/><TextBlock Text="≋   Sound &amp; recording" Margin="14,26,0,0"/><TextBlock Text="⚙   Preferences" Margin="14,26,0,0"/></StackPanel>
 <StackPanel VerticalAlignment="Bottom"><Border BorderBrush="#48584E" BorderThickness="1,0,0,0" Padding="12,0"><StackPanel><TextBlock Text="Your next conversation."/><TextBlock Text="A little more prepared." Style="{StaticResource muted}" Margin="0,5,0,0"/></StackPanel></Border><TextBlock Text="CONCEPT A · STATIC SKETCH" Style="{StaticResource muted}" Margin="0,26,0,0"/></StackPanel>
</DockPanel></Border>
<Grid Grid.Column="1" Margin="32,28"><Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/><RowDefinition/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
 <DockPanel><StackPanel DockPanel.Dock="Right"><TextBlock Text="TEXT DEMO" Foreground="#D9BF8D" HorizontalAlignment="Right"/><TextBlock Text="Microphone off · No API" Style="{StaticResource muted}" Margin="0,5,0,0"/></StackPanel><StackPanel><TextBlock Text="CALL ROOM" Style="{StaticResource muted}"/><TextBlock Text="Client portal discovery" FontSize="28" FontWeight="SemiBold" Margin="0,4,0,0"/></StackPanel></DockPanel>
 <DockPanel Grid.Row="1" Margin="0,26,0,26"><Border Style="{StaticResource action}" DockPanel.Dock="Left"><TextBlock Text="▶  Start call" FontWeight="SemiBold"/></Border><TextBlock Text="Stop    New call" Margin="20,10,0,0" Foreground="#A4B4AD"/><TextBlock Text="Try example    Export notes" HorizontalAlignment="Right" VerticalAlignment="Center"/></DockPanel>
 <Grid Grid.Row="2"><Grid.ColumnDefinitions><ColumnDefinition Width="1.25*"/><ColumnDefinition Width="30"/><ColumnDefinition/></Grid.ColumnDefinitions>
  <Border Background="#172B22" CornerRadius="12" Padding="28"><DockPanel><TextBlock DockPanel.Dock="Top" Text="YOUR NEXT MOVE     /     DISCOVERY" Foreground="#9BDDBA" FontSize="11" Margin="0,0,0,26"/><StackPanel DockPanel.Dock="Bottom"><TextBlock Text="Prepared example · No AI used" Style="{StaticResource muted}" Margin="0,0,0,15"/><Border BorderBrush="#547565" BorderThickness="1" CornerRadius="6" Padding="12,9" HorizontalAlignment="Left"><TextBlock Text="Copy suggested line  ↗"/></Border></StackPanel><StackPanel><TextBlock Text="Make the scope clear" FontSize="17" Foreground="#BCD2C5"/><Border BorderBrush="#BDA57A" BorderThickness="2,0,0,0" Padding="20,0" Margin="0,22,0,0"><TextBlock Text="Who will use the portal, and what should each person be allowed to see?" FontSize="29" LineHeight="39"/></Border></StackPanel></DockPanel></Border>
  <DockPanel Grid.Column="2"><TextBlock DockPanel.Dock="Top" Text="Conversation     /     3 turns" FontSize="16" Margin="0,4,0,25"/><StackPanel><TextBlock Text="CLIENT · 00:32" Style="{StaticResource muted}"/><TextBlock Text="We need a client portal, but we haven't defined who sees what." FontSize="15" LineHeight="23" Margin="0,8,0,24"/><Border BorderBrush="#33433A" BorderThickness="0,1,0,0" Padding="0,20,0,0"><StackPanel><TextBlock Text="YOU · 00:40" Foreground="#8BD9B4" FontSize="12"/><TextBlock Text="Let's walk through how your team works today." FontSize="15" LineHeight="23" Margin="0,8,0,24"/></StackPanel></Border><TextBlock Text="CLIENT · 00:48" Style="{StaticResource muted}"/><TextBlock Text="Sales should see leads. Support only needs existing clients." FontSize="15" LineHeight="23" Margin="0,8,0,0"/></StackPanel></DockPanel>
 </Grid>
 <Border Grid.Row="3" BorderBrush="#33443B" BorderThickness="0,1,0,0" Padding="0,20,0,0" Margin="0,26,0,0"><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="260"/><ColumnDefinition/><ColumnDefinition/><ColumnDefinition/></Grid.ColumnDefinitions><StackPanel><TextBlock Text="Voice &amp; intonation" FontSize="16"/><TextBlock Text="Available with live audio" Style="{StaticResource muted}" Margin="0,5,0,0"/></StackPanel><StackPanel Grid.Column="1"><TextBlock Text="Voice pitch" Style="{StaticResource muted}"/><TextBlock Text="—" FontSize="22"/></StackPanel><StackPanel Grid.Column="2"><TextBlock Text="Speaking pace" Style="{StaticResource muted}"/><TextBlock Text="—" FontSize="22"/></StackPanel><StackPanel Grid.Column="3"><TextBlock Text="Pauses" Style="{StaticResource muted}"/><TextBlock Text="—" FontSize="22"/></StackPanel></Grid></Border>
</Grid>
'@
$b = @'
<Grid.RowDefinitions><RowDefinition Height="86"/><RowDefinition/><RowDefinition Height="86"/></Grid.RowDefinitions>
<Border BorderBrush="#35433B" BorderThickness="0,0,0,1" Padding="32,20"><DockPanel><TextBlock DockPanel.Dock="Left" Text="ARTO /" FontSize="25" Foreground="#9BDDBA" FontWeight="SemiBold" Margin="0,0,48,0"/><TextBlock DockPanel.Dock="Right" Text="Preparation      Sound      Settings" VerticalAlignment="Center"/><StackPanel><TextBlock Text="Client portal discovery" FontSize="22"/><TextBlock Text="CONCEPT B · STATIC SKETCH · TEXT DEMO" Style="{StaticResource muted}"/></StackPanel></DockPanel></Border>
<Grid Grid.Row="1"><Grid.ColumnDefinitions><ColumnDefinition Width="1.15*"/><ColumnDefinition Width="0.85*"/></Grid.ColumnDefinitions>
 <Grid Margin="48,32"><Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition/><RowDefinition Height="Auto"/></Grid.RowDefinitions><TextBlock Text="THE CONVERSATION" Style="{StaticResource muted}"/><StackPanel Grid.Row="1" VerticalAlignment="Center"><TextBlock Text="CLIENT  /  00:32" Foreground="#D9BF8D" FontSize="12"/><TextBlock Text="We need a client portal, but we haven't defined who sees what." FontSize="28" LineHeight="38" Margin="0,12,30,34"/><TextBlock Text="YOU  /  00:40" Foreground="#8BD9B4" FontSize="12"/><TextBlock Text="Let's walk through how your team works today." FontSize="20" LineHeight="30" Margin="0,12,40,34"/><TextBlock Text="CLIENT  /  00:48" Foreground="#D9BF8D" FontSize="12"/><TextBlock Text="Sales should see leads. Support only needs existing clients." FontSize="28" LineHeight="38" Margin="0,12,30,0"/></StackPanel><TextBlock Grid.Row="2" Text="Microphone off · No API · Prepared conversation" Style="{StaticResource muted}"/></Grid>
 <Border Grid.Column="1" Background="#DDE5D9" Padding="40,38"><DockPanel><TextBlock DockPanel.Dock="Top" Text="YOUR NEXT MOVE / 01" Foreground="#416152" FontSize="12" Margin="0,0,0,26"/><StackPanel DockPanel.Dock="Bottom"><Border Background="#1B6548" CornerRadius="6" Padding="16,12"><TextBlock Text="Copy suggested line" HorizontalAlignment="Center"/></Border><TextBlock Text="Prepared example · No AI used" Foreground="#476251" FontSize="12" Margin="0,14,0,0"/></StackPanel><StackPanel><TextBlock Text="Make the scope clear." Foreground="#183B2C" FontSize="40" FontWeight="Light" LineHeight="46"/><TextBlock Text="Who will use the portal, and what should each person be allowed to see?" Foreground="#19372B" FontSize="25" LineHeight="36" Margin="0,34,0,0"/><Border BorderBrush="#A5B7A7" BorderThickness="0,1,0,0" Margin="0,34,0,0" Padding="0,20,0,0"><StackPanel><TextBlock Text="VOICE OBSERVATIONS" Foreground="#476251" FontSize="12"/><TextBlock Text="Waiting for live audio" Foreground="#19372B" Margin="0,8,0,0"/></StackPanel></Border></StackPanel></DockPanel></Border>
</Grid>
<Border Grid.Row="2" Background="#0D1713" Padding="32,20"><DockPanel><Border Style="{StaticResource action}" DockPanel.Dock="Right"><TextBlock Text="▶  Start call" FontWeight="SemiBold"/></Border><TextBlock Text="Try example      New call      Export notes" VerticalAlignment="Center"/><TextBlock Text="No audio is being captured" Style="{StaticResource muted}" HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,30,0"/></DockPanel></Border>
'@
foreach ($concept in @(@{Name='concept-a';Body=$a},@{Name='concept-b';Body=$b})) {
 $xml='<Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="1280" Height="800" Background="#121D18">'+$styles+$concept.Body+'</Grid>'
 $view=[Windows.Markup.XamlReader]::Parse($xml)
 $view.Measure([Windows.Size]::new(1280,800)); $view.Arrange([Windows.Rect]::new(0,0,1280,800)); $view.UpdateLayout()
 $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new(1280,800,96,96,[Windows.Media.PixelFormats]::Pbgra32)
 $bitmap.Render($view)
 $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
 $stream=[IO.File]::Create((Join-Path $output ($concept.Name+'.png')))
 try {$encoder.Save($stream)} finally {$stream.Dispose()}
}
