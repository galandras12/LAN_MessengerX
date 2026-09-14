import QtQuick
import QtQuick.Controls
import QtQuick.Controls.Material
import QtQuick.Layouts
import LanMessenger 1.0

Page {
    id: page

    property string userId: ""
    property string peerName: ""
    property ChatModel chatModel: messenger.chatModelFor(userId)

    title: peerName

    header: ToolBar {
        Material.foreground: "white"
        RowLayout {
            anchors.fill: parent
            anchors.leftMargin: 16
            Label {
                text: page.peerName
                font.pixelSize: 18
                elide: Text.ElideRight
                Layout.fillWidth: true
            }
        }
    }

    ColumnLayout {
        anchors.fill: parent
        spacing: 0

        ListView {
            id: messageList
            Layout.fillWidth: true
            Layout.fillHeight: true
            clip: true
            model: page.chatModel
            spacing: 8

            onCountChanged: positionViewAtEnd()

            delegate: Item {
                width: ListView.view.width
                height: bubble.height + 8

                Rectangle {
                    id: bubble
                    anchors.right: outgoing ? parent.right : undefined
                    anchors.left: outgoing ? undefined : parent.left
                    anchors.margins: 12
                    width: Math.min(label.implicitWidth + 24, page.width * 0.75)
                    height: label.implicitHeight + 16
                    radius: 14
                    color: outgoing ? Material.accentColor : Material.dividerColor

                    Text {
                        id: label
                        anchors.fill: parent
                        anchors.margins: 8
                        text: model.text
                        wrapMode: Text.Wrap
                        color: outgoing ? "white" : Material.foreground
                    }
                }
            }
        }

        RowLayout {
            Layout.fillWidth: true
            Layout.margins: 8

            TextField {
                id: input
                Layout.fillWidth: true
                placeholderText: qsTr("Message…")
                onAccepted: sendButton.clicked()
            }

            Button {
                id: sendButton
                text: qsTr("Send")
                enabled: input.text.length > 0
                onClicked: {
                    messenger.sendMessage(page.userId, input.text)
                    input.text = ""
                }
            }
        }
    }
}
