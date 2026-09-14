import QtQuick
import QtQuick.Controls
import QtQuick.Controls.Material
import QtQuick.Layouts
import LanMessenger 1.0

Page {
    id: page

    property string entryName: ""
    property string entryDate: ""
    property real entryOffset: 0

    title: entryName

    header: ToolBar {
        Material.foreground: "white"
        RowLayout {
            anchors.fill: parent
            anchors.leftMargin: 4
            ToolButton {
                text: "←"
                onClicked: StackView.view.pop()
            }
            ColumnLayout {
                Layout.fillWidth: true
                spacing: 0
                Label {
                    text: page.entryName
                    font.pixelSize: 16
                    elide: Text.ElideRight
                    Layout.fillWidth: true
                }
                Label {
                    text: page.entryDate
                    font.pixelSize: 11
                    opacity: 0.8
                }
            }
        }
    }

    Flickable {
        anchors.fill: parent
        anchors.margins: 16
        contentWidth: width
        contentHeight: content.implicitHeight
        clip: true

        //	The saved payload is raw HTML (see MessengerBridge's history
        //	comment) - the same format Windows' lmcHistoryWindow renders
        //	with pMessageLog->setHtml(data); TextEdit's rich-text mode is
        //	this client's equivalent lightweight HTML renderer.
        TextEdit {
            id: content
            width: parent.width
            text: messenger.historyMessageHtml(page.entryOffset)
            textFormat: TextEdit.RichText
            wrapMode: Text.Wrap
            readOnly: true
            selectByMouse: true
            color: Material.foreground
        }
    }
}
